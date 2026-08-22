# Asset Loader

Reusable, performant, extensible runtime asset loading library for Unity.

RAM cache (LRU + ref-count) + disk cache (ETag/max-age) + request dedup, behind
a small async-first facade. Extensible via interfaces — add a loader, decryptor,
or policy from your own project without touching this package.

## Install

### Via OpenUPM (UniTask dependency)

This package depends on [UniTask](https://github.com/Cysharp/UniTask), distributed via
the OpenUPM scoped registry. Add the registry to your project's `Packages/manifest.json`:

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

### Git dependencies (SiPVLib event bus + log wrapper)

This package also depends on two git-hosted UPM packages with no registry — Unity's
Package Manager does not auto-resolve git dependencies declared inside another package's
`package.json`, so add both explicitly to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.sipvlib.event": "https://github.com/phajmvawnsix/com.sipvlib.event.git",
    "com.sipvlib.debugging": "https://github.com/phajmvawnsix/com.sipvlib.debugging.git"
  }
}
```

- **[com.sipvlib.debugging](https://github.com/phajmvawnsix/com.sipvlib.debugging)** — `CustomLog`
  wrapper over `UnityEngine.Debug`, toggleable via the `LOGGING_DISABLE` scripting define. Backs
  this package's default `IAssetLoaderLogger` (`CustomLogAssetLoaderLogger`).
- **[com.sipvlib.event](https://github.com/phajmvawnsix/com.sipvlib.event)** — type-keyed pub/sub
  bus. Backs this package's default `IAssetLoaderEventSink` (`SipvLibEventAssetLoaderEventSink`),
  which republishes every `AssetLoaderEvent` via `EventManager.Invoke<AssetLoaderEvent>` so a
  decoupled system (debug HUD, analytics, loading-screen controller) can subscribe with
  `EventManager.Add<AssetLoaderEvent>(...)` without referencing the `IAssetLoader` instance
  directly. Use `NoOpAssetLoaderLogger`/`NoOpAssetLoaderEventSink` via
  `AssetLoaderConfigBuilder.UseLogger`/`UseEventSink` to opt out of either.

### Add this package

- **Local path**: `Window > Package Manager > + > Add package from disk...`, select this folder's `package.json`.
- **Git URL**: `Window > Package Manager > + > Add package from git URL...`, paste this repo's URL.

## Quick start

_(added in later steps — API not implemented yet)_

## Run tests

`Window > General > Test Runner > EditMode`, run `SiPV.AssetLoader.Tests`.

## Documentation

See [`Architecture.md`](Architecture.md) (added Step 02) and `docs/step-XX-*.md` for
per-step design notes. Vietnamese translations: `README.vi.md`, `Architecture.vi.md`,
`docs/step-XX-*.vi.md`.
