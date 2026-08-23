# Troubleshooting

Organized by symptom. For what each interface is supposed to do, see
[`IMPLEMENTATION_GUIDE.md`](IMPLEMENTATION_GUIDE.md); for why the pipeline is shaped this way,
see [`ARCHITECTURE.md`](ARCHITECTURE.md).

## Setup and configuration

### `InvalidOperationException: AssetLoaderConfigBuilder.Build() requires UseXxx(...) to be called first`

A mandatory dependency was never set on a hand-built `AssetLoaderConfigBuilder`. The message
names the exact setter you missed (`UseSource`, `UseRamCache`, `UseDiskCache`,
`UseCacheKeyProvider`, `UseTimeoutPolicy`, `UseRetryPolicy`, or `UseCachePolicy`). Either call
that setter, or start from `AssetLoaderConfigBuilder.CreateDefault()` instead, which pre-fills
every mandatory slot and still lets you override individual ones before `.Build()`.

### `InvalidOperationException: AssetLoader.Default was read before SetDefault was called`

Something called `AssetLoader.LoadAsync`/`AssetLoader.Default` before your bootstrap code ran
`AssetLoader.SetDefault(...)`. There is no implicit fallback instance on purpose, since a loader
needs a cache directory and budget decision only the host project can make. Check that your
bootstrap component's `Awake`/`Start` (or equivalent) actually runs, and runs, before the first
script that loads an asset (Unity script execution order, or an explicit initialization step, if
this is timing-sensitive).

## Handles and memory

### `ObjectDisposedException` reading `handle.Asset`

This particular `AssetHandle<T>` instance was already released (`Release()`/`Dispose()` called on
it). If you still need the asset, hold a separate handle via `Retain()` before releasing the
first one, rather than trying to reuse a released instance.

### Assets never freed / memory keeps growing across loads

Almost always a missing `Release()`/`Dispose()` somewhere. Check:

- Every `LoadAsync` call site has a matching release, including on exception paths (`using var
  handle = ...` is the safest form).
- Every `Retain()` call has its own matching release: retaining creates an additional owner that
  must release independently.
- Inspect `handle.RefCount` at a breakpoint or log line; if it never reaches 0, something is still
  holding a reference.
- Subscribe an `IAssetLoaderEventSink` and count `CacheMiss` events per key over time; a key that
  keeps re-fetching from the source instead of hitting `RamCacheHit` suggests the RAM entry keeps
  getting evicted, possibly because nothing is retaining it and `TrimToBudget` is reclaiming it
  between loads, which is expected if that's genuinely how many live loads you have.

### RAM cache budget seems too small / evicting things I still need

`IRamCache.TrimToBudget()` only evicts entries with zero live handles; if you're seeing evictions
of assets you still expect to be in use, the handle for that asset was probably already released
somewhere. If the budget itself is just too tight for your working set, raise `ramMaxEntries`/
`ramMaxBytes` via `AssetLoaderConfigBuilder.CreateDefault(...)` or the hand-built `RamCache`
constructor.

## Load failures

`AssetLoadException.ErrorCode` tells you which stage failed; branch on it rather than parsing the
message.

### `FetchFailed`

The source could not deliver bytes. Check `AssetLoadException.HttpStatusCode`:

- **Null**: the request never got an HTTP response at all (DNS failure, connection refused,
  network unreachable). Generally worth retrying (`DefaultRetryPolicy` already does, for this
  case).
- **Non-null, 5xx or a genuinely unknown status**: also retried by default.
- **Non-null, 4xx**: not retried by default (a 404 retrying itself is pointless). Check the URL
  and any auth headers (`AssetRequest.CustomHeaders`).
- **304 with no prior metadata**: `"Source returned 304 Not Modified with no known prior metadata
  to revalidate against."` means the source answered a conditional GET the pipeline never
  actually sent an `ETag` for, or the disk metadata was cleared between the conditional request
  being built and the response arriving. Usually indicates the disk cache and metadata store have
  drifted, see the Caching section below.

### `ProcessingFailed`

A registered `IContentProcessor` threw. Common cause: a decrypt-style processor given the wrong
key (check `AssetRequest.UserData` is actually being forwarded and read correctly on both the
encrypt and decrypt sides), or a processor applied unconditionally to content it was never meant
to transform (see the demo's `ConditionalProcessor` pattern in `IMPLEMENTATION_GUIDE.md` section
4 for how to scope a processor to specific requests).

### `DecodeFailed`

Either no decoder matched the requested type/content, or the matching decoder threw.

- **"No decoder registered for T"**: you called `LoadAsync<T>` for a type with no
  `RegisterDecoder<T>(...)` call in your builder setup, or the response's content type / URL
  extension didn't match any registered decoder's `CanDecode`. Check both the decoder is
  registered and the source actually returns a content type or the URL has a recognizable
  extension.
- **Decoder threw on valid-looking bytes**: usually means the bytes don't actually match the
  declared type (corrupt download, wrong URL, or a content processor upstream that should have
  decrypted/decompressed them but didn't run or ran with the wrong key). Check the processor
  chain ran successfully before blaming the decoder.

### `TimedOut`

Either a single attempt exceeded `ITimeoutPolicy.GetTimeout(request)`, or the cumulative time
across retries exceeded `GetOverallDeadline(request)` (if one is set; the default policy leaves
it unbounded). Raise the per-attempt timeout for genuinely slow sources or large payloads, or
check whether the deadline itself is the intended cap on total wait time.

## Caching issues

### Disk cache appears to never hit, always refetches from network

- **Metadata/content store drift**: confirm every write and every eviction touches both
  `IDiskCache` and `IDiskCacheMetadataStore`. If you're calling either store directly (outside
  the pipeline, e.g. for manual cache management) and only touch one, the other one falls out of
  sync. The pipeline itself always keeps them paired; drift usually comes from custom code
  bypassing it.
- **`Cache-Control: no-store` on the response**: strips the returned ETag/max-age, so the entry
  is never treated as revalidatable even though the bytes were still written to disk. Check the
  actual response headers your source is sending.
- **Missing ETag**: `CacheEntryMetadata.IsFresh` treats a null `MaxAge` as never fresh, so content
  served with no cache headers at all revalidates (or worse, misses if there's also no ETag) on
  every single load. Confirm your CDN or server actually sends `Cache-Control`/`ETag`.
- **`AssetRequestFlags.ForceRefetch` set on the request**: this flag skips revalidation entirely
  by design; remove it if you expect normal caching behavior.

### More network requests happening than expected

Check for a cache-key mismatch: `AssetRequest.Variant` or a differing query string changes the
resolved cache seed (`ResolveCacheSeed()`), so two requests that look like "the same asset" to a
human can resolve to two different cache entries. Log `ICacheKeyProvider.GetRamKey`/`GetDiskKey`
for the requests in question and compare.

## Performance

### Frame hitches when loading a texture, audio clip, or asset bundle

Expected for these three: `Texture2D.LoadImage`, `UnityWebRequestMultimedia`-based audio decode,
and `AssetBundle.LoadFromMemoryAsync` all must run on the main thread (Unity offers no
thread-pool-safe path for any of them), so decode cost for these is real main-thread frame time
proportional to the asset's size. Schedule large loads during a loading screen, or stagger
several loads across frames, rather than firing them all mid-gameplay at once.

## Cancellation

### `OperationCanceledException` even though I didn't cancel anything

Check whether the timeout policy fired instead: a per-attempt timeout is implemented as a linked
`CancellationTokenSource`, so it surfaces as `OperationCanceledException` to the operation the
same way an explicit cancellation would. The pipeline itself converts a timeout-sourced
cancellation into an `AssetLoadException` with `ErrorCode = TimedOut` before it reaches your
`catch` block, so if you're catching `OperationCanceledException` directly around the whole
`LoadAsync` call, check you're not accidentally catching this converted case, or check for
`AssetLoadException` first.

### Cancelling one of several concurrent loads for the same URL also seems to cancel the others

It shouldn't: dedup coalescing uses per-caller cancellation for each individual waiter and a
separate shared token for the underlying fetch, which only cancels once every currently
interested caller has cancelled. If you're seeing all callers cancel together, check whether
they're all sharing the exact same `CancellationTokenSource` at the call site rather than
independent tokens.

## Unity-specific gotchas

- **`Destroy` vs `DestroyImmediate` in EditMode tests**: `UnityEngine.Object.Destroy` is a
  no-op-until-next-frame in Play mode but logs an error when called outside Play mode (Editor
  tooling, EditMode tests). Code that needs to run correctly in both contexts should branch on
  `Application.isPlaying` (see `Texture2DDecoder`'s failure-cleanup path for the pattern).
- **Test Runner failing a test because of an unrelated logged error**: an `IAssetLoaderLogger`
  call or an internal `Debug.LogError` during a test that expects failure needs
  `LogAssert.Expect(...)` in the test, or NUnit's Test Runner integration fails the test purely
  for the log output even if every assertion passed.
- **`UnityWebRequest` main-thread requirement**: any custom `IAssetSource`/`IHttpClient` backed by
  `UnityWebRequest` must be invoked from the main thread. The pipeline already guarantees this
  before calling into `IAssetSource.FetchAsync`, but if you're calling a custom source directly
  outside the pipeline (e.g. in a test), you'll need to switch to the main thread yourself first.
- **Git dependencies not resolving**: `com.sipvlib.event` and `com.sipvlib.debugging` are declared
  inside this package's own `package.json`, but Unity's Package Manager does not resolve git
  dependencies declared transitively inside another package. Both must also be listed directly in
  your own project's `Packages/manifest.json` (see the README's Install section).

## How to debug a load that isn't behaving as expected

1. Subscribe an `IAssetLoaderEventSink` (or temporarily swap in a logging one via `UseEventSink`)
   and read the reported `AssetLoaderEventKind` for the request in question: it tells you exactly
   which tier served the load (`RamCacheHit`, `DiskCacheHit`, `DiskCacheRevalidated`, `CacheMiss`,
   `DedupCoalesced`) or where it failed (`LoadFailed`), rather than guessing from the outside.
2. If it's a `LoadFailed`, catch the `AssetLoadException` and log `ErrorCode`, `RequestKey`, and
   `HttpStatusCode` together; branch your fix based on `ErrorCode` per the Load failures section
   above.
3. For a caching question specifically, inspect the cache directory layout directly:
   `Application.persistentDataPath/asset-cache` (or your configured `cacheDirectoryPath`) holds
   the raw content files, keyed by the SHA256 hash `DefaultCacheKeyProvider.GetDiskKey` produces;
   cross-reference against `IDiskCacheMetadataStore.GetAllAsync()` to see what metadata the
   pipeline currently believes exists.
4. For a dedup question, watch the HTTP request count (the demo's `CountingHttpClient` decorator
   in `Samples~/Demo/` is a working example of wrapping `IHttpClient` purely for this kind of
   diagnostic) alongside `DedupCoalesced` events to confirm concurrent callers are actually
   sharing one fetch.
5. If none of the above narrows it down, reproduce the failing load path in isolation against the
   demo scene (`Samples~/Demo/DemoScene.unity`) using its "Load any asset at runtime" section,
   which exercises the exact same `LoadAsync<T>` call your code does, with the event log visible
   in real time.
