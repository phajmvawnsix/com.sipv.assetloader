using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // no-op processor; useful when a consumer wants to bypass processing for one asset type mid-chain
    // without ripping it out of the config (pipeline already short-circuits the "no processors" case itself)
    public sealed class PassThroughContentProcessor : IContentProcessor
    {
        public UniTask<byte[]> ProcessAsync(byte[] input, AssetProcessingContext context, CancellationToken cancellationToken) =>
            UniTask.FromResult(input);
    }
}
