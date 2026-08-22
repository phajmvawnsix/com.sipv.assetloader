using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>Entry point of the package - async facade over the pipeline (dedup, RAM cache, disk cache, source fetch, processing, decode). Build instances via AssetLoaderConfigBuilder.</summary>
    public interface IAssetLoader
    {
        /// <summary>
        /// Loads an asset, going through the cache tiers before hitting the source.
        /// Call from the main thread; it always resumes there too, so the returned handle's
        /// Asset can be used immediately without re-syncing.
        /// </summary>
        /// <param name="request">What to load, plus any per-request policy overrides.</param>
        /// <returns>
        /// A ref-counted handle the caller owns. Release it when done or the asset stays pinned
        /// in the RAM cache for the rest of the session.
        /// </returns>
        UniTask<AssetHandle<T>> LoadAsync<T>(AssetRequest request, CancellationToken cancellationToken = default);

        // Warms the cache without handing back a live handle - loads then releases internally. Good for prefetch.
        UniTask PreloadAsync(AssetRequest request, CancellationToken cancellationToken = default);

        // Takes the full request (not just a key) so the RAM/disk keys are derived the same way LoadAsync
        // would, respecting Key/Variant overrides - otherwise the wrong entry gets invalidated. Handles
        // already issued to callers stay valid until released.
        UniTask InvalidateAsync(AssetRequest request, CancellationToken cancellationToken = default);
    }
}
