using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>Abstraction over where an asset's bytes come from (HTTP by default; consumers can plug in local bundles, custom CDNs, etc via AssetLoaderConfigBuilder).</summary>
    public interface IAssetSource
    {
        // UnityWebRequest-based implementations must be called on main thread (pipeline guarantees this);
        // pipeline re-syncs threads itself before the next stage, don't worry about it here
        UniTask<AssetSourceResult> FetchAsync(AssetSourceRequestContext context, CancellationToken cancellationToken);
    }
}
