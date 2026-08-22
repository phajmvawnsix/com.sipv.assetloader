using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Concrete pipeline-backed IAssetLoader. Construct via `new AssetLoaderService(builder.Build())`
    // and optionally register it as the static AssetLoader.Default.
    public sealed class AssetLoaderService : IAssetLoader
    {
        private readonly AssetLoaderConfig _config;
        private readonly IAssetLoadPipeline _pipeline;

        public AssetLoaderService(AssetLoaderConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _pipeline = new AssetLoadPipeline(_config, new InFlightRequestCoordinator());
        }

        public UniTask<AssetHandle<T>> LoadAsync<T>(AssetRequest request, CancellationToken cancellationToken = default)
        {
            return _pipeline.ExecuteAsync<T>(request, cancellationToken);
        }

        public UniTask PreloadAsync(AssetRequest request, CancellationToken cancellationToken = default)
        {
            return _pipeline.PreloadAsync(request, cancellationToken);
        }

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
