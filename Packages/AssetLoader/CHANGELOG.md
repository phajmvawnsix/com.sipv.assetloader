# Changelog

All notable changes to this package are documented in this file.

## \[0.1.0] - Unreleased

* Async-first `IAssetLoader.LoadAsync<T>` facade, plus a static `AssetLoader` convenience wrapper
  for projects without a DI container.
* Two-tier caching: synchronous, main-thread, ref-counted RAM cache (`IRamCache`/`RamCache`) in
  front of an async, thread-pool disk cache (`IDiskCache`/`FileDiskCache` with a separate
  `IDiskCacheMetadataStore`/`FileDiskCacheMetadataStore`).
* HTTP-aware revalidation via `ETag`/`If-None-Match` and `Cache-Control: max-age` parsing in the
  default `HttpAssetSource`.
* Request dedup: concurrent loads for the same key are coalesced onto one in-flight fetch via
  `IInFlightRequestCoordinator`, with asymmetric per-caller/shared-group cancellation.
* Ref-counted `AssetHandle<T>` ownership model: `Retain()`/`Release()`/`Dispose()`, no finalizer,
  double-release is a safe no-op.
* Configurable timeout, retry (exponential backoff with jitter), and cache-freshness policies
  (`ITimeoutPolicy`, `IRetryPolicy`, `ICachePolicy`), each overridable globally or per request.
* Built-in decoders: `byte[]`, `string`, `Texture2D`, `Sprite`, `AudioClip`, `AssetBundle`.
* Pluggable extensibility points: `IAssetSource`, `IHttpClient`, `IAssetDecoder<T>`,
  `IContentProcessor`, all three policies, `IAssetLoaderLogger`, `IAssetLoaderEventSink`, wired
  together via `AssetLoaderConfigBuilder`.
* `AssetLoaderConfigBuilder.CreateDefault()` factory: a pre-populated builder using every built-in
  implementation, still overridable before `.Build()`.
* Diagnostics: `IAssetLoaderEventSink` telemetry (`RamCacheHit`, `DiskCacheHit`,
  `DiskCacheRevalidated`, `CacheMiss`, `DedupCoalesced`, `RetryAttempted`, `LoadFailed`) and
  `IAssetLoaderLogger`, both backed by SiPVLib integrations by default with no-op opt-outs.
* Interactive OnGUI demo sample proving every cache tier, dedup, and all three extensibility
  points against the real pipeline, with Inspector-tunable cache and policy settings.
* Full XML documentation across the public API surface.

