using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Persistent byte-oriented cache tier, content only - metadata lives in IDiskCacheMetadataStore.
    // Stores raw bytes exactly as received, before IContentProcessor runs, so encrypted-at-rest assets stay
    // encrypted on disk. All I/O-bound, must not touch UnityEngine APIs, callers are already off the main
    // thread. Content and metadata being separate stores means every write/evict here needs the matching
    // call on the metadata store or the two drift apart.
    public interface IDiskCache
    {
        // miss returns Found=false, doesn't throw
        UniTask<DiskCacheReadResult> TryReadAsync(string diskKey, CancellationToken cancellationToken);

        UniTask WriteAsync(string diskKey, byte[] content, CancellationToken cancellationToken);

        UniTask EvictAsync(string diskKey, CancellationToken cancellationToken);

        UniTask<long> GetTotalSizeBytesAsync(CancellationToken cancellationToken);

        // LRU over the metadata store's LastAccessUtc
        UniTask TrimToBudgetAsync(CancellationToken cancellationToken);
    }
}
