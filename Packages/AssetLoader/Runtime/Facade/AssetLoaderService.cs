using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// The package's <see cref="IAssetLoader"/> implementation, backed by the full load pipeline
    /// (dedup, RAM cache, disk cache, source fetch, content processing, decode).
    /// </summary>
    /// <remarks>
    /// Build one at app bootstrap:
    /// <code>
    /// var loader = new AssetLoaderService(AssetLoaderConfigBuilder.CreateDefault().Build());
    /// </code>
    /// Instances are independent: each owns its own in-flight coordinator, so two instances
    /// pointed at the same cache directory will not coalesce each other's requests. One instance
    /// per app is the norm. Named differently from the static <see cref="AssetLoader"/> facade to
    /// avoid a type-name collision.
    /// </remarks>
    public sealed class AssetLoaderService : IAssetLoader
    {
        private readonly AssetLoaderConfig _config;
        private readonly IAssetLoadPipeline _pipeline;

        /// <summary>Creates a loader over an already-built config.</summary>
        /// <param name="config">Resolved dependencies, from <see cref="AssetLoaderConfigBuilder.Build"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
        public AssetLoaderService(AssetLoaderConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _pipeline = new AssetLoadPipeline(_config, new InFlightRequestCoordinator());
        }

        /// <inheritdoc />
        public UniTask<AssetHandle<T>> LoadAsync<T>(AssetRequest request, CancellationToken cancellationToken = default)
        {
            return _pipeline.ExecuteAsync<T>(request, cancellationToken);
        }

        /// <inheritdoc />
        public UniTask PreloadAsync(AssetRequest request, CancellationToken cancellationToken = default)
        {
            return _pipeline.PreloadAsync(request, cancellationToken);
        }

        /// <inheritdoc />
        public async UniTask InvalidateAsync(AssetRequest request, CancellationToken cancellationToken = default)
        {
            var ramKey = _config.CacheKeyProvider.GetRamKey(request);
            var diskKey = _config.CacheKeyProvider.GetDiskKey(request);

            _config.RamCache.Evict(ramKey);
            await _config.DiskCache.EvictAsync(diskKey, cancellationToken);
            await _config.MetadataStore.RemoveAsync(diskKey, cancellationToken);
        }
    }
}
