# Asset Loader

A reusable, extensible runtime asset loading library for Unity 2022.3+. It gives you a single
async `LoadAsync<T>` call that transparently handles RAM caching, disk caching with HTTP-style
revalidation, request dedup, retries, and decoding, so you stop writing that plumbing by hand in
every project.

## Why

Loading remote or local assets at runtime usually means rebuilding the same handful of pieces
over and over: a cache so you don't refetch the same URL twice, dedup so five simultaneous
requests for the same asset don't fire five downloads, retry/timeout handling, and a decode step
per asset type. This package packages all of that behind one facade, while keeping every piece
swappable through interfaces so you're not locked into its defaults.

## Features

| Feature | What it means |
|---|---|
| **Async-first facade** | `AssetLoader.LoadAsync<T>(url)` returns a `UniTask<AssetHandle<T>>`. No callbacks, no coroutines required. |
| **Two-tier caching** | A synchronous, main-thread RAM cache (LRU, ref-counted) in front of an async, thread-pool disk cache. |
| **HTTP-aware revalidation** | Disk entries are revalidated via `ETag`/`If-None-Match`; a `304` reuses the cached bytes instead of re-downloading. |
| **Request dedup** | Concurrent callers loading the same key share one in-flight fetch instead of firing one each. |
| **Ref-counted handles** | `AssetHandle<T>` tracks how many callers are holding an asset; it's only released once the count hits zero. |
| **Pluggable everything** | Sources, decoders, content processors, and timeout/retry/cache policies are all interfaces, swappable per-app or per-request. |
| **Built-in decoders** | `byte[]`, `string`, `Texture2D`, `Sprite`, `AudioClip`, `AssetBundle` out of the box. |

## Install

### 1. Add this package

Pick one:

- **GitHub UPM (git URL)**: `Window > Package Manager > + > Add package from git URL...`, then
  paste:

  ```
  https://github.com/phajmvawnsix/com.sipv.assetloader.git?path=Packages/AssetLoader
  ```

  The `?path=` suffix is required: this repository is a full Unity project, and the package
  itself lives in the `Packages/AssetLoader` subfolder rather than at the repo root. Without it,
  Package Manager will look for a `package.json` at the repo root and fail to resolve.

  To pin a specific version instead of tracking the branch head, append a `#` revision, for
  example a tag or commit hash:

  ```
  https://github.com/phajmvawnsix/com.sipv.assetloader.git?path=Packages/AssetLoader#v0.1.0
  ```

- **Local path**: `Window > Package Manager > + > Add package from disk...`, then select this
  folder's `package.json`. Useful when developing against a local clone.

### 2. Add its dependencies

This package cannot resolve its own dependencies transitively, since Unity's Package Manager
does not follow git or scoped-registry dependencies declared inside another package's
`package.json`. Add all three to your own project's `Packages/manifest.json`.

**UniTask**, via the OpenUPM scoped registry:

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.cysharp.unitask"]
    }
  ]
}
```

**SiPVLib event bus and log wrapper**, via git URL (no registry):

```json
{
  "dependencies": {
    "com.sipvlib.event": "https://github.com/phajmvawnsix/com.sipvlib.event.git",
    "com.sipvlib.debugging": "https://github.com/phajmvawnsix/com.sipvlib.debugging.git"
  }
}
```

- **[com.sipvlib.debugging](https://github.com/phajmvawnsix/com.sipvlib.debugging)** backs this
  package's default `IAssetLoaderLogger` (`CustomLogAssetLoaderLogger`), a thin wrapper over
  `UnityEngine.Debug` that can be silenced globally via the `LOGGING_DISABLE` scripting define.
- **[com.sipvlib.event](https://github.com/phajmvawnsix/com.sipvlib.event)** backs this package's
  default `IAssetLoaderEventSink` (`SipvLibEventAssetLoaderEventSink`), which republishes every
  `AssetLoaderEvent` onto a pub/sub bus so a decoupled system (loading HUD, analytics) can
  subscribe without touching `IAssetLoader` directly.

Swap either out with `NoOpAssetLoaderLogger` / `NoOpAssetLoaderEventSink` via
`AssetLoaderConfigBuilder.UseLogger` / `UseEventSink` if you'd rather not take those two
dependencies at all.

## Quick start

```csharp
using SiPV.AssetLoader;
using UnityEngine;

var config = AssetLoaderConfigBuilder.CreateDefault().Build();
AssetLoader.SetDefault(new AssetLoaderService(config));

using var handle = await AssetLoader.LoadAsync<Texture2D>("https://example.com/image.png");
var texture = handle.Asset;
```

`CreateDefault()` wires up the HTTP source, RAM and disk caches, default policies, and every
built-in decoder in one call, and returns a builder so you can still override any single piece
before calling `.Build()`. See [`IMPLEMENTATION_GUIDE.md`](IMPLEMENTATION_GUIDE.md) for hand-built
configuration and how to replace individual pieces.

## The one rule: release your handles

`AssetHandle<T>` is ref-counted, not garbage collected. A `Texture2D` still bound to a live
material has no way for the cache to know it's still needed, so the caller has to say so
explicitly:

```csharp
var handle = await AssetLoader.LoadAsync<Texture2D>(url);
try
{
    ApplyToMaterial(handle.Asset);
}
finally
{
    handle.Release(); // or: using var handle = await ...
}
```

There is no finalizer safety net for a leaked handle on purpose: a finalizer calling back into
the cache from the GC thread would be worse than the leak it prevents. Forgetting `Release()` (or
`Dispose()`, which does the same thing) means the underlying asset is never freed. Double-release
is safe and a no-op, so it's fine to call it defensively.

## Adding it to an existing project without a DI container

1. Build one `AssetLoaderConfig` for your whole app, typically in a bootstrap `MonoBehaviour` or
   static initializer that runs before anything calls `LoadAsync`.
2. Wrap it in an `AssetLoaderService` and register it with `AssetLoader.SetDefault(...)` so the
   static `AssetLoader` facade works from anywhere without passing the service around.
3. Call `AssetLoader.LoadAsync<T>(...)` from any script. If you'd rather inject the service
   explicitly instead of using the static facade, construct and pass your own
   `AssetLoaderService` (or `IAssetLoader`) reference instead.

## Running the tests

`Window > General > Test Runner > EditMode`, run the `SiPV.AssetLoader.Tests` assembly.

## Trying the demo

Import the **Demo** sample from this package's entry in `Window > Package Manager`, then open
`Samples/Asset Loader/<version>/Demo/DemoScene.unity` and press Play. It's a single-file, OnGUI
scene (no Canvas/Button wiring) proving every cache tier, dedup, and all three extensibility
points against the real pipeline: load/reload, clear RAM, clear disk, load several assets
concurrently, load an arbitrary local file or URL at runtime, and a custom decoder/processor/retry
policy. Every serialized field on `DemoController` (RAM and disk budgets, timeout, retry count,
concurrent-load count) is tunable from the Inspector. See `Samples~/Demo/README.md` for what each
button proves.

## Documentation

| Document | Covers |
|---|---|
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Component map, data structures, runtime flow diagrams, threading and cancellation model, performance characteristics, design trade-offs. |
| [`IMPLEMENTATION_GUIDE.md`](IMPLEMENTATION_GUIDE.md) | Per-subsystem usage: configuration, handle lifetime, custom decoders/processors/policies/sources, cache tiers, diagnostics. |
| [`TROUBLE_SHOOTING.md`](TROUBLE_SHOOTING.md) | Common errors, what causes them, and how to debug a load that isn't behaving as expected. |

## License

Apache-2.0. See [`LICENSE`](LICENSE).
