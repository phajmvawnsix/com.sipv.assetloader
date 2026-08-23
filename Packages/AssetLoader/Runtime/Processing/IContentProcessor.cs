using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>Bytes-in/bytes-out transform between raw fetch and decode (decrypt, decompress, etc). Runs on the thread pool - no UnityEngine API calls.</summary>
    public interface IContentProcessor
    {
        /// <summary>Transforms bytes, for example decrypting or decompressing them, and returns the transformed result.</summary>
        /// <param name="input">The bytes from the previous stage: raw fetch output, or the previous processor's output.</param>
        /// <param name="context">The URL, content type, and any caller payload for this asset.</param>
        /// <param name="cancellationToken">Cancels the in-flight transform.</param>
        UniTask<byte[]> ProcessAsync(byte[] input, AssetProcessingContext context, CancellationToken cancellationToken);
    }
}
