# Implementation guide

Per-subsystem usage: what it does, how to use the built-in implementation, how to replace or
extend it correctly, and what the contract requires so a custom implementation doesn't get
subtly wrong. For the reasoning behind the design and how data flows end to end, see
[`ARCHITECTURE.md`](ARCHITECTURE.md).

Every extensibility point below has a live reference implementation in the demo sample's
`Samples~/Demo/Extensibility/` folder: `XorContentProcessor.cs`, `SingleRetryPolicy.cs`, and
`MyDataDecoder.cs`, each proven against the real pipeline rather than just described here.

## Table of contents

1. [Configuration and bootstrap](#1-configuration-and-bootstrap)
2. [Loading and handle lifetime](#2-loading-and-handle-lifetime)
3. [Custom decoders](#3-custom-decoders)
4. [Content processors](#4-content-processors)
5. [Policies](#5-policies)
6. [Sources and transport](#6-sources-and-transport)
7. [Cache tiers](#7-cache-tiers)
8. [Cache keys](#8-cache-keys)
9. [Diagnostics](#9-diagnostics)

## 1. Configuration and bootstrap

Build one `AssetLoaderConfig` per app, once, before anything calls `LoadAsync`.

**Fastest path**, everything built-in:

```csharp
var config = AssetLoaderConfigBuilder.CreateDefault().Build();
AssetLoader.SetDefault(new AssetLoaderService(config));
```

`CreateDefault()` takes optional parameters for the four things most projects actually want to
tune without hand-building anything: `cacheDirectoryPath`, `diskBudgetBytes`, `ramMaxEntries`,
`ramMaxBytes`. It returns a builder, not a finished config, so you can still override any
individual slot before calling `.Build()`:

```csharp
var config = AssetLoaderConfigBuilder
    .CreateDefault(diskBudgetBytes: 128L * 1024 * 1024)
    .UseRetryPolicy(new MyCustomRetryPolicy())
    .Build();
```

**Hand-built**, when you want every slot explicit:

```csharp
var config = new AssetLoaderConfigBuilder()
    .UseSource(new HttpAssetSource(new UnityWebRequestHttpClient()))
    .UseRamCache(new RamCache(new DefaultMemorySizeEstimator(), new DefaultAssetReleaser(), maxEntries: 64, maxBytes: 64L * 1024 * 1024))
    .UseDiskCache(diskCache, metadataStore)
    .UseCacheKeyProvider(new DefaultCacheKeyProvider())
    .UseTimeoutPolicy(new DefaultTimeoutPolicy())
    .UseRetryPolicy(new DefaultRetryPolicy())
    .UseCachePolicy(new DefaultCachePolicy())
    .RegisterDecoder(new Texture2DDecoder())
    .Build();
```

`Build()` throws `InvalidOperationException` naming the exact setter you skipped if a mandatory
dependency was never set (`UseSource`, `UseRamCache`, `UseDiskCache`, `UseCacheKeyProvider`,
`UseTimeoutPolicy`, `UseRetryPolicy`, `UseCachePolicy`). `UseLogger` and `UseEventSink` are the
only optional setters; skipping them falls back to `CustomLogAssetLoaderLogger.Instance` and
`SipvLibEventAssetLoaderEventSink.Instance` respectively.

**Registering the built config**, `AssetLoader.SetDefault(new AssetLoaderService(config))` wires
up the static `AssetLoader.LoadAsync<T>(...)` facade. If you use a DI container instead, register
`AssetLoaderService` (or the `IAssetLoader` interface it implements) as a singleton and inject it
normally; `AssetLoader.SetDefault` is entirely optional and nothing else in the package depends on
it.

## 2. Loading and handle lifetime

```csharp
using var handle = await AssetLoader.LoadAsync<Texture2D>(new AssetRequest(url));
rawImage.texture = handle.Asset;
```

`AssetHandle<T>` is ref-counted, not garbage collected: `Release()` (or `Dispose()`, which calls
it) is mandatory. Forgetting it pins the asset in the RAM cache for the rest of the session, since
there is no finalizer safety net by design (a finalizer calling Unity destroy APIs from the GC
thread is unsupported and worse than the leak). Double-release on the same instance is a
warning-logged no-op, not a throw, so a defensive extra release in a teardown path is safe.

Handing the same asset to a second owner that will release independently: call `Retain()` rather
than sharing the original handle instance.

```csharp
var handle = await AssetLoader.LoadAsync<Texture2D>(request);
var sharedWithOtherSystem = handle.Retain(); // separate release, same underlying asset
```

Typed shortcuts exist for the built-in asset types (`LoadTextureAsync`, `LoadAudioClipAsync`,
`LoadTextAsync`, `LoadBytesAsync` in `AssetLoaderExtensions`), each a one-line forward to
`LoadAsync<T>`. Use the generic call directly whenever you need policy overrides, a custom cache
key, or request flags.

`AssetRequest` fields worth knowing beyond `Url`:

- `Key`/`Variant`: override what the caches key off. Use `Key` when the URL itself is unstable but
  the content is not (a signed URL whose token changes per session); use `Variant` to distinguish
  multiple cached versions of the same source (two quality levels of the same texture URL).
- `Flags` (`AssetRequestFlags`, bitwise-combinable): `BypassRamCache`, `BypassDiskCache`,
  `ForceRevalidate` (conditional GET even if still fresh), `ForceRefetch` (skip revalidation,
  redownload unconditionally).
- `TimeoutPolicyOverride`/`RetryPolicyOverride`/`CachePolicyOverride`: per-request policy
  overrides, each falling back to the loader's global default when null.
- `UserData`: an opaque payload forwarded untouched to your own content processors and decoders,
  for example a decryption key.

`PreloadAsync(request)` warms the disk tier only, no decode and no RAM population, useful for
prefetching a batch during a loading screen. `InvalidateAsync(request)` drops both cache tiers'
entries for a request so the next load refetches; handles already issued stay valid until
released, it only stops new loads from resolving to the stale entry.

## 3. Custom decoders

```csharp
public interface IAssetDecoder<T>
{
    bool CanDecode(Type targetType, string contentTypeOrExtension);
    UniTask<T> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken);
}
```

Register via `AssetLoaderConfigBuilder.RegisterDecoder<T>(decoder)`. Resolution checks every
registered decoder for `T` against the content type first, then the URL extension; the registry
also lets a later registration for the same `T` win over an earlier one, which is the supported
way to override a built-in decoder (say, a custom `Texture2D` decoder using different import
settings) without forking package source.

Contract a custom decoder must honor:

- **Must return on the main thread.** The pipeline switches to the main thread once before
  `RamCache.Put`, as a defensive no-op if the decoder is already there, but it does not switch
  *for* the decoder mid-flight. A decoder that touches UnityEngine APIs (constructing a
  `Texture2D`, calling `AssetBundle.LoadFromMemoryAsync`) must switch there itself before doing
  so; a decoder for pure data (`byte[]`, `string`) can skip threading entirely.
- **Throw `AssetLoadException` with `ErrorCode = DecodeFailed`** on bad input, rather than
  returning a default/null value silently. A caller branching on `ErrorCode` should be able to
  trust that a successful `DecodeAsync` return is actually usable.
- **Cancellation is best-effort where the underlying Unity API offers no abort.**
  `AssetBundleDecoder` is the reference example: `AssetBundle.LoadFromMemoryAsync` cannot be
  aborted once started, so a cancelled decode stops *awaiting* it but keeps a background
  continuation alive to unload the bundle if it does finish, rather than leaking it.
- **Live Unity resources need a matching `IAssetReleaser`.** The default releaser calls
  `Object.Destroy`, which is correct for `Texture2D`/`AudioClip` but wrong for an `AssetBundle`
  (needs `AssetBundle.Unload`). Register a custom `IAssetReleaser` alongside a decoder that
  returns something with different cleanup needs.

`AssetDecodeContext` carries `Url`, `ContentType`, `Extension` (lowercase, no leading dot, query
string and fragment stripped), and `UserData` from the request.

## 4. Content processors

```csharp
public interface IContentProcessor
{
    UniTask<byte[]> ProcessAsync(byte[] input, AssetProcessingContext context, CancellationToken cancellationToken);
}
```

Register via `AssetLoaderConfigBuilder.AddContentProcessor(processor)`. Processors run in
registration order, each output feeding the next input; an empty chain is a pass-through. Use this
for bytes-in/bytes-out transforms between fetch and decode: decryption, decompression, checksum
verification.

Contract: **must run entirely on the thread pool**, no UnityEngine API calls. The disk cache
stores bytes *before* processing runs (raw as fetched), so an encrypted-at-rest project keeps
ciphertext on disk and rotating the decryption key never invalidates the cache; if your processor
needs a key, pass it through `AssetRequest.UserData` rather than hardcoding it.

The demo's `Extensibility/XorContentProcessor.cs` shows a real example, including a
`ConditionalProcessor` wrapper pattern (in `DemoController.cs`) for applying a processor only to
requests carrying a marker, since a processor registered directly runs unconditionally over every
load in the pipeline.

`PassThroughContentProcessor` is a shipped no-op, useful when you want to bypass processing for
one asset type mid-chain without removing it from the config entirely.

## 5. Policies

All three ship a default and accept a global registration (`UseTimeoutPolicy`/`UseRetryPolicy`/
`UseCachePolicy`) plus a per-request override (`AssetRequest.TimeoutPolicyOverride`/
`RetryPolicyOverride`/`CachePolicyOverride`, each null-falls-back to the global one).

**`ITimeoutPolicy`**: `GetTimeout(request)` returns the per-attempt timeout;
`GetOverallDeadline(request)` returns an optional wall-clock cap across all retries combined
(null means unbounded, retries are then limited only by the retry policy's attempt count).
`DefaultTimeoutPolicy` takes `perAttemptTimeout` (default 15s) and `overallDeadline` (default
null) in its constructor.

**`IRetryPolicy`**: `ShouldRetry(RetryContext)` returns a `RetryDecision` (`Retry(delay)` or
`Stop()`). `RetryContext` carries `Request`, `AttemptNumber` (1-based, the attempt that just
failed), `LastException`, and `Stage` (`RetryStage.Fetch`/`Process`/`Decode`).
`DefaultRetryPolicy` only retries `Fetch` failures (a processing or decode failure is
deterministic given the same bytes, so retrying would just fail the same way again), specifically
timeouts and 5xx-or-unknown-status responses, not 4xx client errors. Exponential backoff with
jitter: `maxAttempts` (default 3), `baseDelay` (default 500ms, doubling per attempt), `maxDelay`
(default 10s cap), `jitterFraction` (default 0.2, randomized fraction added to avoid retry
storms across many clients). The demo's `Extensibility/SingleRetryPolicy.cs` shows a minimal
alternative: one retry, no backoff.

**`ICachePolicy`**: `Evaluate(request, existingMetadata, nowUtc)` returns a `CacheLookupResult`
(`Fresh`, `StaleRevalidate`, or `Miss`). `DefaultCachePolicy` returns `Miss` when there is no
existing metadata or `AssetRequestFlags.ForceRefetch` is set, `StaleRevalidate` when
`ForceRevalidate` is set or the entry is past its `MaxAge`, and `Fresh` otherwise.

## 6. Sources and transport

`IAssetSource.FetchAsync(context, cancellationToken)` is where bytes come from on a cache miss or
revalidation. `AssetSourceRequestContext` carries `Url`, `ETagIfKnown`/`LastModifiedIfKnown` (for
a conditional GET), `CustomHeaders`, `TimeoutPolicy`, `Priority`. Return an `AssetSourceResult`
with `Status` set to `Ok200` (full content), `NotModified304` (revalidation confirmed, no body),
or `Failed` (with `Error` set, not thrown, so the pipeline's retry logic sees it uniformly).

Contract: **must be called on the main thread** if the implementation uses `UnityWebRequest`
(the pipeline guarantees this before calling in) and does not need to switch back before
returning, since the pipeline re-syncs threads itself before the next stage.

The default, `HttpAssetSource`, wraps an injected `IHttpClient` and owns all the conditional-GET
and `Cache-Control` parsing: sends `If-None-Match`/`If-Modified-Since` when known, parses the
response's `ETag`/`Cache-Control: max-age` into the result. `Cache-Control: no-store` on the
response strips the returned ETag and max-age so the entry is not treated as revalidatable (note:
the pipeline still writes the raw bytes to disk regardless, see the Limitations section of
`ARCHITECTURE.md`).

`IHttpClient` is the transport underneath `HttpAssetSource`, kept as its own interface so the
conditional-GET logic can be tested without a real `UnityWebRequest`. The default,
`UnityWebRequestHttpClient`, must also run on the main thread.

Replace `IAssetSource` entirely to load from a non-HTTP source (local `AssetBundle` server,
custom CDN protocol, an in-memory fixture for tests): implement the interface directly and
register it via `UseSource`, no need to go through `IHttpClient` at all if HTTP semantics don't
apply.

## 7. Cache tiers

**`IRamCache`**: synchronous, main-thread-only by contract, holds decoded ref-counted assets.
`TryGet<T>(ramKey, out handle)` on a hit bumps the ref count and hands out a new handle; a miss
(including a key cached under a different type) returns false. `Put<T>(ramKey, asset, metadata)`
stores a freshly decoded asset and returns the caller's own handle at ref count 1: it does not
expect a follow-up `TryGet`, which would double the ref count. `Evict(ramKey)` removes an entry
unconditionally from lookup (existing handles stay valid until released); `TrimToBudget()`
evicts unreferenced entries least-recently-used first until back inside budget, skipping anything
with a live handle. Because eviction only ever touches unreferenced entries, this is an LRU among
unreferenced entries, not a true global LRU: a pinned asset is never destroyed out from under its
owner, even under budget pressure.

Do not add locking to a custom `IRamCache` implementation; call it from the main thread instead,
same as the built-in `RamCache`.

**`IDiskCache`** + **`IDiskCacheMetadataStore`**: async, thread-pool, I/O-bound, and deliberately
two separate stores so either can be swapped independently (loose files vs. a key-value store for
metadata, for example). `IDiskCache` holds raw bytes exactly as fetched, before content
processing. `WriteAsync` must be atomic from a reader's perspective: a concurrent or
crash-interrupted read should see either the old content or the new one, never a half-written
file (the built-in `FileDiskCache` does this via write-to-temp-then-move). The critical rule when
implementing or calling either store directly: **every write or eviction on one needs the
matching call on the other**, or they drift, metadata claiming a fresh entry whose bytes are
gone, or orphaned bytes no lookup can reach. This is the first thing to check when disk cache
behavior looks wrong; see `TROUBLE_SHOOTING.md`.

## 8. Cache keys

`ICacheKeyProvider.GetRamKey(request)` / `GetDiskKey(request)` both start from
`AssetRequest.ResolveCacheSeed()` (`Key` or `Url`, plus `:Variant` if set). `DefaultCacheKeyProvider`
returns the RAM key raw (readable in a debugger, only ever a dictionary key) and SHA256-hashes the
disk key to a hex string (must survive being a filename on every target platform, which a raw URL
cannot: character restrictions and path-length limits vary by platform). Override this interface
to add cache-busting, for example prefixing the seed with an app or content version so a new
release invalidates every cached entry at once.

## 9. Diagnostics

`IAssetLoaderLogger` (`LogWarning`/`LogError`) and `IAssetLoaderEventSink` (`Report(AssetLoaderEvent)`)
are both optional builder setters (`UseLogger`/`UseEventSink`) with SiPVLib-backed defaults
(`CustomLogAssetLoaderLogger`, `SipvLibEventAssetLoaderEventSink`) and no-op opt-outs
(`NoOpAssetLoaderLogger.Instance`, `NoOpAssetLoaderEventSink.Instance`).

`AssetLoaderEvent.Kind` (`RamCacheHit`, `DiskCacheHit`, `DiskCacheRevalidated`, `CacheMiss`,
`DedupCoalesced`, `RetryAttempted`, `LoadFailed`) tells you which tier actually served a load
without instrumenting call sites yourself. Events report on whatever thread the pipeline happens
to be on when they fire, not necessarily the main thread, so a UI consumer (like the demo's event
log) needs its own thread-safe hand-off if it reads the events from `OnGUI`/`Update`.

```csharp
public sealed class MyEventSink : IAssetLoaderEventSink
{
    public void Report(in AssetLoaderEvent loaderEvent) =>
        Debug.Log($"{loaderEvent.Kind}: {loaderEvent.Key} ({loaderEvent.Duration}ms)");
}
```
