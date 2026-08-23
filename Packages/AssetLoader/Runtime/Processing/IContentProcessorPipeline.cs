using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>Runs the registered <see cref="IContentProcessor"/> chain in order; an empty chain is a pass-through.</summary>
    public interface IContentProcessorPipeline
    {
        /// <summary>Runs the raw bytes through every registered processor in registration order.</summary>
        /// <param name="rawBytes">The bytes as returned by the source or read from disk cache.</param>
        /// <param name="context">The URL, content type, and any caller payload for this asset.</param>
        /// <param name="cancellationToken">Cancels the chain; a processor mid-run is expected to observe it too.</param>
        /// <returns>The fully processed bytes, ready for decoding.</returns>
        UniTask<byte[]> RunAsync(byte[] rawBytes, AssetProcessingContext context, CancellationToken cancellationToken);
    }
}
