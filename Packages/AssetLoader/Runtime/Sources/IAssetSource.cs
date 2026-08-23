using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>Abstraction over where an asset's bytes come from (HTTP by default; consumers can plug in local bundles, custom CDNs, etc via AssetLoaderConfigBuilder).</summary>
    public interface IAssetSource
    {
        /// <summary>Fetches an asset's raw bytes, honoring conditional-GET revalidation if the context carries an ETag.</summary>
        /// <remarks>
        /// <c>UnityWebRequest</c>-based implementations must be called on the main thread,
        /// which the pipeline guarantees. The pipeline re-syncs threads itself before the next
        /// stage, so an implementation does not need to switch back before returning.
        /// </remarks>
        /// <param name="context">The request, including the cache key and any known ETag to revalidate against.</param>
        /// <param name="cancellationToken">Cancels the in-flight fetch.</param>
        /// <returns>
        /// An <see cref="AssetSourceResult"/> whose <see cref="AssetSourceStatus"/> reports success,
        /// a 304 not-modified, or a failure, never an exception for an ordinary fetch failure
        /// (those are carried in <see cref="AssetSourceResult.Error"/>).
        /// </returns>
        UniTask<AssetSourceResult> FetchAsync(AssetSourceRequestContext context, CancellationToken cancellationToken);
    }
}
