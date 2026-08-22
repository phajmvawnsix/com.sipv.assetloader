using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Runs the registered IContentProcessor chain in order; empty chain is a pass-through.
    public interface IContentProcessorPipeline
    {
        UniTask<byte[]> RunAsync(byte[] rawBytes, AssetProcessingContext context, CancellationToken cancellationToken);
    }
}
