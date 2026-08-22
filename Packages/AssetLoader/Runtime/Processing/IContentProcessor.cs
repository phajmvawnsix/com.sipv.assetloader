using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>Bytes-in/bytes-out transform between raw fetch and decode (decrypt, decompress, etc). Runs on the thread pool - no UnityEngine API calls.</summary>
    public interface IContentProcessor
    {
        UniTask<byte[]> ProcessAsync(byte[] input, AssetProcessingContext context, CancellationToken cancellationToken);
    }
}
