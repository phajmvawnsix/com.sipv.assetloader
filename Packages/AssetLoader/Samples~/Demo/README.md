# AssetLoader Demo

Interactive, visual proof of every core feature: cache tiers (RAM/disk), request dedup,
loading arbitrary runtime input (local path or web URL, any type), and 3 Open/Closed
extensibility examples (custom decoder, custom content processor, custom policy) - all
proven live against the real pipeline, not just described in a README. Runs entirely
through `OnGUI` (no Canvas/Button scene wiring) so the whole demo is one script attached
to one GameObject - the scene file has nothing to go missing or break on import.

This is the package's only sample - the 3 extensibility examples used to be separate
importable samples, but since this demo already proves them live against a real
running pipeline, keeping them as a second, static copy elsewhere was redundant. Their
source now lives in `Extensibility/` right next to `DemoController.cs`, in the same
assembly, so you can read the actual classes the buttons below call into.

## Running it from zero

1. **Import this sample.** In your Unity project, `Window > Package Manager > Asset
   Loader > Samples > Demo > Import`. This copies `Samples~/Demo/` into
   `Assets/Samples/Asset Loader/<version>/Demo/`.
2. **Open `DemoScene.unity`** (in the imported Demo folder) and press Play.

That's it - **no server, no setup**. The default Image URL is a public one
(`picsum.photos`), so every button works immediately.

If you want to point at a different image, edit the `Image Url` field on the
`DemoController` component in the Inspector before pressing Play.

## What each button proves

| Button | Proves |
|---|---|
| **Load** | First click: full miss, goes through source fetch, disk write, RAM populate - watch the event log for `CacheMiss`. Click it again immediately: `RamCacheHit`, load time drops to near-0ms. |
| **Clear RAM cache** | Evicts just the RAM entry (releases the demo's own handle first, so the eviction can actually run per `IRamCache.Evict`'s ref-count contract). Click **Load** again: `DiskCacheHit` or `DiskCacheRevalidated` (304) in the event log, no full network fetch. |
| **Clear disk cache** | Empties every entry via `IDiskCache.EvictAsync` + `IDiskCacheMetadataStore.RemoveAsync` together (they're separate stores, both need clearing or they drift). Click **Load** again after this (and after Clear RAM): full `CacheMiss`, real network fetch. |
| **Load 5 concurrent** | Fires 5 `LoadAsync` calls for the same URL without awaiting between them. The HTTP request counter (bottom of the demo panel) barely moves - 0 or 1 new requests for 5 callers, not 5. That's `IInFlightRequestCoordinator` coalescing them into one real fetch. Watch for 4 `DedupCoalesced` events in the log. |
| **Cache stats line** | RAM entries/bytes are tracked by the demo itself (`IRamCache` intentionally has no stats accessor). Disk entries/bytes come straight from the real cache via `AssetLoaderConfig.DiskCache`/`MetadataStore`, which the demo holds a direct reference to since it built the config itself. |
| **Event log** | Every `AssetLoaderEvent` the pipeline reports, newest first - the same `IAssetLoaderEventSink` hook a production analytics integration would use. |
| **Load any asset at runtime** | Type a path/URL into the text field (or click **Browse...**, Editor-only, opens a native file dialog filtered to the picked type), pick a type (Texture2D/AudioClip/Text/MyData) with the toolbar, click Load. A bare path with no `http(s)://`/`file://` prefix is treated as a local device path and converted to a `file://` URI - `UnityWebRequest` (what `HttpAssetSource` runs on) reads `file://` URIs directly, so a local file goes through the exact same source/cache/decode pipeline as a web URL, no separate code path. **MyData** type: click **Create demo .mydata file** first (writes a throwaway file with a `.mydata` extension), then Load - this goes through `Extensibility/MyDataDecoder.cs`, a decoder registered entirely from outside the package's own `Runtime/` code. |
| **Load XOR-encrypted demo text** | Encrypts a fixed demo string with `Extensibility/XorContentProcessor.cs`'s real `XorContentProcessor` (XOR is symmetric, so running it once on plain bytes IS the encrypt step), writes the encrypted bytes to a local temp file, then loads that file through the real pipeline - the same processor decrypts it automatically on the way through. Status line confirms the decrypted text matches the original. A demo-only `ConditionalProcessor` wrapper (registered in `Awake()`, not part of the extensibility sample itself) makes sure XOR only applies to this one demo file, not every other asset the demo loads. |
| **Use SingleRetryPolicy toggle / Load a guaranteed-500 URL** | Loads `https://httpstat.us/500` (a public endpoint that always returns HTTP 500, a well-known service for exactly this kind of test) through `IRetryPolicy`. With the toggle off, `DefaultRetryPolicy` retries 3 times with exponential backoff; with it on, `Extensibility/SingleRetryPolicy.cs`'s real `SingleRetryPolicy` retries exactly once with no delay via `AssetRequest.retryPolicyOverride`. Compare the elapsed time and the `RetryAttempted` count in the event log between the two. |

## `Extensibility/` - what to copy into your own project

Each file is a complete, working example of one extensibility point. Copy the pattern,
not necessarily the exact class, into your own project's assembly:

- **`MyDataAsset.cs` / `MyDataDecoder.cs`** - a fake `.mydata` format standing in for a
  real custom asset type (fbx, usd, a proprietary level format, etc). The pattern shown
  doesn't depend on what the real format actually is: implement `IAssetDecoder<T>`,
  register it via `AssetLoaderConfigBuilder.RegisterDecoder<T>`. For a real binary
  format, swap `DecodeAsync`'s body for the real parser call, keep `UnityEngine` API
  calls (mesh/texture creation) on main thread the same way `Texture2DDecoder` does,
  update `CanDecode`'s match strings.
- **`XorContentProcessor.cs`** - deliberately the simplest possible transform, to keep
  the example about the registration pattern (`IContentProcessor` +
  `AssetLoaderConfigBuilder.AddContentProcessor`) rather than about cryptography. XOR
  is symmetric, so this one processor doubles as both the encrypt step (offline, when
  you produce the cached bytes) and the decrypt step (here, when the pipeline reads
  them back). **Production note:** use AES (or your platform's crypto library) instead
  - XOR-with-a-fixed-key is trivially reversible by anyone who extracts the app.
- **`SingleRetryPolicy.cs`** - retries exactly once, immediately, no backoff. Not
  something worth shipping as a package default (most consumers want
  `DefaultRetryPolicy`'s exponential backoff), but a realistic ask for something like
  an unskippable splash-screen asset, where waiting through a full backoff before
  giving up defeats the point. Usable globally (`AssetLoaderConfigBuilder.
  UseRetryPolicy`) or per-request (`AssetRequest.retryPolicyOverride`), same as any
  `IRetryPolicy`.

## Integration cost this demo doesn't show

This demo builds its own `AssetLoaderConfig` from scratch (`Awake()`) to keep the whole
thing self-contained in one file. For how little code a *real* integration into an
existing project actually needs, see the "5-minute integration" section in the package's
main `README.md` - it's the same builder calls, just without the demo's extra
instrumentation (`CountingHttpClient`, the tracked RAM stats dictionary, the event log).
