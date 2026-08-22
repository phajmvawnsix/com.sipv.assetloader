using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Internal: dedup -> RAM cache -> disk cache/ETag -> fetch -> process -> decode -> populate caches.
    // Not implementable by consumers, they use AssetLoaderConfigBuilder instead.
    public interface IAssetLoadPipeline
    {
        // Runs the pipeline for one request, returns a ref-counted handle.
        UniTask<AssetHandle<T>> ExecuteAsync<T>(AssetRequest request, CancellationToken cancellationToken);
    }
}
