using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>No-op <see cref="IContentProcessor"/> that returns its input unchanged.</summary>
    /// <remarks>
    /// Useful when a consumer wants to bypass processing for one asset type mid-chain without
    /// removing it from the config. Note the pipeline already short-circuits the "no processors
    /// registered at all" case on its own, so this is only needed for a chain with other real
    /// processors in it.
    /// </remarks>
    public sealed class PassThroughContentProcessor : IContentProcessor
    {
        /// <inheritdoc />
        public UniTask<byte[]> ProcessAsync(byte[] input, AssetProcessingContext context, CancellationToken cancellationToken) =>
            UniTask.FromResult(input);
    }
}
