using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Runs the full load pipeline: dedup, RAM cache, disk cache/ETag, fetch, content processing,
    /// decode, cache populate.
    /// </summary>
    /// <remarks>
    /// Not meant to be implemented by consumers: configure behavior through
    /// <see cref="AssetLoaderConfigBuilder"/> instead of replacing this interface.
    /// </remarks>
    public interface IAssetLoadPipeline
    {
        /// <summary>Runs the pipeline for one request and returns a ref-counted handle.</summary>
        /// <param name="request">The request to load, including its cache key, flags, and policy overrides.</param>
        /// <param name="cancellationToken">Cancels this caller's wait. See dedup cancellation semantics on <see cref="IInFlightRequestCoordinator"/>.</param>
        UniTask<AssetHandle<T>> ExecuteAsync<T>(AssetRequest request, CancellationToken cancellationToken);

        /// <summary>Warms the disk tier only. There is no target type to decode into, so no decode or RAM-cache step runs.</summary>
        /// <param name="request">The request to preload.</param>
        /// <param name="cancellationToken">Cancels the preload.</param>
        UniTask PreloadAsync(AssetRequest request, CancellationToken cancellationToken);
    }
}
