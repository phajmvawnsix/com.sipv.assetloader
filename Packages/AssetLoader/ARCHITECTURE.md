# Architecture

This document describes how the package is put together internally: the component map, the data
structures that flow through it, the runtime paths a load can take, and the trade-offs behind the
non-obvious decisions. For "how do I use X" or "how do I replace X", see
[`IMPLEMENTATION_GUIDE.md`](IMPLEMENTATION_GUIDE.md) instead.

## Component map

```
                         AssetLoader (static facade)
                                  │
                          AssetLoaderService  ── implements IAssetLoader
                                  │
                          IAssetLoadPipeline  ── internal, owns the whole flow
                                  │
        ┌─────────────────────────┼─────────────────────────┐
        │                         │                          │
IInFlightRequestCoordinator   IRamCache               (below, per-request)
  (dedup, internal)          (sync, main thread)
                                  │
                            IDiskCacheMetadataStore ── freshness/ETag bookkeeping
                                  │
                            IDiskCache  ── raw bytes, pre-processing
                                  │
                            IAssetSource  ── HTTP by default (IHttpClient underneath)
                                  │
                      IContentProcessorPipeline ── chain of IContentProcessor
                                  │
                      IAssetDecoderRegistry → IAssetDecoder<T>
```

Everything below `AssetLoaderService` is an interface wired together by
`AssetLoaderConfigBuilder`, the composition root. `IAssetLoadPipeline` and
`IInFlightRequestCoordinator` are internal: consumers configure behavior through the builder's
setters, not by implementing those two directly. Every other interface in the map
(`IAssetSource`, `IRamCache`, `IDiskCache`, `IDiskCacheMetadataStore`, `ICacheKeyProvider`,
`IAssetDecoder<T>`, `IContentProcessor`, `ITimeoutPolicy`, `IRetryPolicy`, `ICachePolicy`,
`IAssetLoaderLogger`, `IAssetLoaderEventSink`) is a real extension point with a shipped default
implementation.

## Data structures

### `AssetRequest`

The input to a load: `Url`, an optional `Key`/`Variant` pair that overrides what the caches key
off, `Priority`, `Flags` (`AssetRequestFlags`, a bitwise-combinable enum for cache bypass and
forced refresh), per-request policy overrides (`TimeoutPolicyOverride`/`RetryPolicyOverride`/
`CachePolicyOverride`, each falling back to the loader's global default when null),
`CustomHeaders`, and an opaque `UserData` payload forwarded untouched to content processors and
decoders. A readonly struct: passing one around costs nothing and there is no null case.

`ResolveCacheSeed()` builds the string both cache tiers key off: `Key` if set, otherwise `Url`,
with `Variant` appended after a colon when present. Allocation-free in the common case (no
variant): it returns the existing string reference rather than concatenating.

### `AssetHandle<T>`

Ref-counted ownership token returned by every load. `Asset` (the decoded value), `Key` (the RAM
cache key), `IsValid`/`RefCount` for introspection, `Retain()` (hands out a second handle sharing
the same count), and `Release()`/`Dispose()` (gives up this instance's reference; the last release
fires an internal callback the RAM cache uses to make the entry eviction-eligible).

State machine: **create** (ref count 1, returned from a load) → **retain** (ref count +1 per
call, for handing the same asset to a second owner) → **release** (ref count -1 per call, once
per instance, double-release on the same instance is a no-op that logs a warning rather than
throwing) → **zero-ref** (ref count hits 0, the RAM cache tier is notified and the entry becomes
eligible for LRU eviction, though it is not destroyed immediately: a load of the same key before
the next eviction pass can still reuse it).

### `CacheEntryMetadata`

Freshness and bookkeeping persisted alongside cached bytes: `ETag`, `MaxAge` (nullable; null means
the server said nothing about lifetime, treated as never fresh rather than cached forever),
`FetchedAtUtc`, `LastAccessUtc` (drives LRU ordering, refreshed via `WithLastAccess` on every
read), `SizeBytes`, `ContentType`. Kept as a separate struct from the bytes themselves so the disk
cache's two stores (content, metadata) can be swapped independently of each other.

### `AssetSourceResult`

What an `IAssetSource.FetchAsync` call returns: `Status` (`AssetSourceStatus`: `Ok200`,
`NotModified304`, `Failed`), `RawBytes` (null on `NotModified304`/`Failed`), `ETag`, `MaxAge`
(parsed from `Cache-Control: max-age`), `FetchedAtUtc`, `ContentType`, and `Error` (set only on
`Failed`). An ordinary fetch failure is carried in `Error`, not thrown, so the pipeline's retry
logic can inspect it uniformly alongside processing/decode failures.

### `RetryContext` / `RetryDecision`

`RetryContext` describes a failure to `IRetryPolicy.ShouldRetry`: `Request`, `AttemptNumber`
(1-based, the attempt that just failed), `LastException`, `Stage` (`RetryStage`: `Fetch`,
`Process`, or `Decode`). `RetryDecision` is the policy's answer: `Retry(delay)` or `Stop()`.

### `AssetLoaderEvent`

Lightweight telemetry: `Kind` (`AssetLoaderEventKind`: `RamCacheHit`, `DiskCacheHit`,
`DiskCacheRevalidated`, `CacheMiss`, `DedupCoalesced`, `RetryAttempted`, `LoadFailed`), `Key`,
`Duration` (nullable, since not every event kind has a meaningful timing). Reported through
`IAssetLoaderEventSink` on whatever thread the pipeline happens to be on when the event fires,
never marshalled to the main thread first.

## Cache key derivation

`ICacheKeyProvider.GetRamKey` returns the resolved cache seed (`AssetRequest.ResolveCacheSeed()`)
verbatim. `GetDiskKey` SHA256-hashes the same seed to a hex string. The RAM key stays raw
because it is only ever a dictionary key and a readable one is far easier to follow in a debugger
or log line. The disk key must survive being a filename on every target platform, which a raw URL
does not: mobile filesystems reject characters URLs use freely and impose path length limits URLs
routinely exceed, so a fixed-length hash sidesteps both problems at once.

## Runtime flow

Every path below is triggered by `IAssetLoadPipeline.ExecuteAsync<T>`, which `AssetLoaderService`
calls under the hood.

### 1. RAM hit

```
[main] ExecuteAsync
  → RamCache.TryGet<T>(ramKey) succeeds
  → report RamCacheHit
  → return handle (RefCount++)
```

Fully synchronous, no thread switch, no allocation beyond the returned handle. This is the fast
path every repeat load for the same key takes once it has been loaded once.

### 2. Disk hit, no revalidation needed

```
[main]  ExecuteAsync: RamCache miss, no in-flight fetch for this key
[main]  Register with the dedup coordinator
[pool]  SwitchToThreadPool
[pool]  MetadataStore.GetAsync → metadata found
[pool]  CachePolicy.Evaluate → Fresh (still within MaxAge)
[pool]  DiskCache.TryReadAsync → bytes found, report DiskCacheHit
[pool]  ProcessorPipeline.RunAsync (chain, or pass-through if empty)
[pool]  DecoderRegistry.TryResolve<T> → decoder found
[?]     decoder.DecodeAsync (may run on the pool, must finish on main thread)
[main]  SwitchToMainThread (defensive no-op if the decoder already did)
[main]  RamCache.Put → handle returned, dedup coordinator resolves any coalesced waiters
```

No network round trip at all: freshness is decided purely from the locally stored `MaxAge`/
`FetchedAtUtc`.

### 3. Disk hit via revalidation (304)

Same as above except `CachePolicy.Evaluate` returns `StaleRevalidate` (entry exists but is past
`MaxAge`, or the request set `ForceRevalidate`). The pipeline then does a conditional GET
(`If-None-Match: <ETag>`) instead of trusting the local bytes outright:

```
[pool]  CachePolicy.Evaluate → StaleRevalidate
[main]  SwitchToMainThread (IAssetSource contract)
[main]  Source.FetchAsync with If-None-Match: <known ETag>
[main]  → 304 Not Modified
[pool]  SwitchToThreadPool
[pool]  DiskCache.TryReadAsync → reuse existing bytes, report DiskCacheRevalidated
[pool]  MetadataStore.SetAsync → refresh LastAccessUtc (and MaxAge, if the 304 response sent one)
[pool → main]  process, decode, RAM-populate as above
```

If the source instead answers `200 OK` (content actually changed), the flow merges into the full
miss path below from the fetch step onward.

### 4. Full miss

```
[pool]  CachePolicy.Evaluate → Miss (no metadata, or ForceRefetch flag set)
[main]  Source.FetchAsync (unconditional GET)
[main]  → 200 OK
[pool]  DiskCache.WriteAsync (raw bytes, pre-processing, written even if the request set
        BypassDiskCache: that flag only skips the read side, not the write side)
[pool]  MetadataStore.SetAsync, report CacheMiss
[pool]  process, decode, RAM-populate as above
```

### 5. Concurrent dedup coalescing

```
Caller A: ExecuteAsync → RAM miss, no in-flight fetch → Register, starts LoadAndCacheAsync
Caller B: ExecuteAsync → RAM miss, TryGetExisting finds A's in-flight fetch
          → report DedupCoalesced, await A's shared task, Retain() the resulting handle
```

Both callers end up with independent `AssetHandle<T>` instances sharing one ref count increment
each, from a single real fetch. Cancellation is asymmetric on purpose: caller A cancelling its own
token does not cancel B's wait, and the underlying shared fetch only actually cancels once every
currently-interested caller has cancelled. `IInFlightRequestCoordinator.Register` returns a
`sharedToken` reflecting that combined state, separate from any individual caller's own token.

## Threading model

| Where | What runs there | Why |
|---|---|---|
| Main thread | `IAssetSource.FetchAsync` (via `UnityWebRequest`), `IRamCache` (all methods), decoder implementations that touch UnityEngine objects (`Texture2D.LoadImage`, `AssetBundle.LoadFromMemoryAsync`, `UnityWebRequestMultimedia`), the final `RamCache.Put` | `UnityWebRequest` and most UnityEngine object construction only work on the main thread; `IRamCache` is deliberately synchronous so it never allocates a `UniTask` on the hottest path. |
| Thread pool | Cache metadata lookups, disk I/O (`IDiskCache`, `IDiskCacheMetadataStore`), `IContentProcessorPipeline` | None of these touch UnityEngine APIs, so keeping them off the main thread avoids blocking frame time on I/O. |
| Either, decoder's choice | `IAssetDecoder<T>.DecodeAsync` | The contract only requires finishing on the main thread; a decoder for a pure-data type (`byte[]`, `string`) never needs to switch at all. |

The pipeline switches threads explicitly around each boundary (`UniTask.SwitchToThreadPool` /
`SwitchToMainThread`) rather than assuming a decoder or source implementation will do it, except
for the final switch before `RamCache.Put`, which is a defensive no-op if the decoder already
left execution on the main thread.

## Cancellation model

Two independent cancellation surfaces exist:

- **Per-caller**: the `CancellationToken` passed to `LoadAsync`/`ExecuteAsync`. Cancelling it
  stops that caller's own wait (`AttachExternalCancellation`) without affecting a shared in-flight
  fetch other callers are still coalesced onto.
- **Shared-group**: the token `IInFlightRequestCoordinator.Register` hands back alongside
  registration. It only transitions to cancelled once every currently-interested caller's own
  token has cancelled, since one caller giving up must not abort a fetch others are still waiting
  on.

A per-attempt timeout (`ITimeoutPolicy.GetTimeout`) is implemented as a linked
`CancellationTokenSource` wrapping the caller's token with `CancelAfter`, so a timeout and an
explicit cancellation both surface as `OperationCanceledException` to the operation itself; the
pipeline distinguishes them by checking whether the caller's own token was the one that fired
before converting a timeout into an `AssetLoadException` with `ErrorCode = TimedOut`.

## Performance characteristics

No measured numbers here (they depend entirely on target hardware, network, and cached asset
sizes), only where the design spends or avoids cost and why:

- **RAM hit path allocates nothing but the returned handle.** No `UniTask` machinery is entered
  since `IRamCache.TryGet` is a plain synchronous call.
- **Every load allocates at least one `AssetHandle<T>`** (and one per `Retain()`), by design: it
  is the ownership token, and there is no cheaper way to represent "N independent owners of one
  cached asset" without it.
- **The async state machine cost is unavoidable** for any path that leaves the RAM tier (disk I/O
  or network), since `UniTask` still boxes/allocates for genuinely asynchronous continuations even
  though it avoids `Task`'s allocation in the synchronous-completion case.
- **Main-thread decode cost is real and asset-size-dependent.** `Texture2D.LoadImage` and
  `AssetBundle.LoadFromMemoryAsync` both cost main-thread frame time proportional to the asset,
  since Unity does not expose a thread-pool-safe path for either. A large texture or bundle
  decoded mid-frame can produce a visible hitch; stagger large loads across frames or trigger them
  during a loading screen if this matters for your project.
- **Disk reads are whole-file** (`FileDiskCache` reads an entire cached entry into one `byte[]`
  before handing it to the processor chain). Fine for typical texture/audio/bundle sizes; a
  streaming read path would be needed before this package could handle much larger entries
  without a memory spike per read.
- **Measuring it yourself**: subscribe an `IAssetLoaderEventSink` and read `AssetLoaderEvent.Duration`
  for per-stage timing already captured by the pipeline, or wrap a call site in
  `System.Diagnostics.Stopwatch` for wall-clock time, or sample `GC.GetTotalMemory(false)` before
  and after a batch of loads to see allocation pressure in your own project's actual usage
  pattern. None of these numbers are meaningful outside your target device and content, which is
  why none are reproduced here.

## Design decisions and trade-offs

| Decision | Rationale | Cost accepted |
|---|---|---|
| Ref-counted `AssetHandle<T>`, no finalizer | The cache has no way to know whether a caller still needs a `Texture2D`/`AudioClip`; only the caller does. A finalizer calling back into the cache from the GC thread would be worse than the leak it prevents. | Forgetting `Release()` leaks the asset for the rest of the session; there is no automatic safety net. |
| Two cache tiers with deliberately different shapes | `IRamCache` must be synchronous and allocate nothing on the hottest path; `IDiskCache` is inherently I/O-bound and async. Forcing one shape onto both was rejected as strictly worse for both use cases. | Two interfaces to implement instead of one, and two sets of tests. |
| Content and metadata as separate disk stores | Lets storage technology for either be swapped independently (e.g. metadata in a fast key-value store, content as loose files). | Every write or eviction needs its counterpart call on the other store, or the two drift; this is the first thing to check when disk cache behavior looks wrong. |
| RAM key raw, disk key hashed | RAM key is a dictionary key a developer will actually read in a debugger; disk key must be a valid, length-bounded filename on every target platform. | An extra SHA256 computation per disk-tier operation; negligible relative to the I/O itself. |
| One `AssetLoadException` type with an `ErrorCode` enum, not an exception hierarchy | Callers almost always want "was this the network or the content," which a hierarchy of six exception types would force into six `catch` blocks. | Less type-level precision than a full hierarchy; `ErrorCode` plus `HttpStatusCode` covers what callers have actually needed. |
| Dedup coalescing with asymmetric cancellation | One caller giving up must not abort a fetch others are still depending on. | More cancellation-token bookkeeping in the pipeline than a naive "cancel the shared task on any cancel" implementation. |
| Last-registered-wins for decoders | Lets a consumer override a built-in decoder (say, a custom `Texture2D` decoder with different import settings) without forking package source. | No compile-time signal when two registrations for the same type collide; only the later one is used, silently. |
| `Build()` throws naming the missing setter rather than defaulting silently | A misconfigured loader should fail at bootstrap, not on the first cache miss in front of a player. | Hand-built configs (as opposed to `CreateDefault()`) require every mandatory setter to be called explicitly. |
| No measured performance targets shipped in this document | Real numbers depend on target hardware, network conditions, and asset sizes the package cannot know in advance; a number chosen without those constraints would be misleading. | Consumers must profile in their own project rather than trust a number here. |

## Limitations and future work

- No `Last-Modified` persistence: `CacheEntryMetadata` only tracks `ETag`, so a source that omits
  ETag but supports `If-Modified-Since` gets no revalidation benefit from this package today.
- `Cache-Control: no-store` suppresses the returned `ETag`/`MaxAge`, but the raw bytes are still
  written to the disk tier by the pipeline's normal write step; a source that truly must never
  persist bytes at rest needs its own encryption or a custom `IDiskCache` that no-ops the write.
- `FileDiskCache` reads an entire cached entry into memory per read; no streaming read path exists
  yet for entries large enough that this matters.
- No `Application.lowMemory` hook wired into the RAM cache; eviction only happens on the normal
  budget-exceeded path, not in response to a platform memory-pressure signal.
- `AudioType.MPEG` (mp3) decode support is inconsistent across platforms per Unity's own
  documentation; verify mp3 playback on every target platform rather than assuming Editor/desktop
  behavior carries over.
- Main-thread decode cost for `Texture2D`/`AssetBundle`/`AudioClip` is unavoidable with the
  current Unity APIs available on the 2022.3 LTS floor this package targets.
- `AssetDecoderRegistry.TryResolve` calls `CanDecode` on each candidate up to twice per resolution
  in the worst case; not a correctness issue, just a known inefficiency in the resolution loop.
- No built-in support for progress reporting mid-download; a source implementation that wants to
  expose progress needs its own side channel, since `IAssetSource.FetchAsync` only returns a
  final result.
- No CDN-specific cache-header quirks are special-cased; `CacheEntryMetadata.IsFresh` treats a
  missing `MaxAge` as never fresh, which is correct per plain HTTP semantics but may not match
  every CDN's actual intended default.
