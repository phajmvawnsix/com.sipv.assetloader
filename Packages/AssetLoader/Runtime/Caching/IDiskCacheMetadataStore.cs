using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Persists CacheEntryMetadata keyed by disk cache key, kept separate from IDiskCache so storage strategy can be swapped independently.
    public interface IDiskCacheMetadataStore
    {
        // null = no metadata recorded, treat as a cache miss
        UniTask<CacheEntryMetadata?> GetAsync(string diskKey, CancellationToken cancellationToken);

        UniTask SetAsync(string diskKey, CacheEntryMetadata metadata, CancellationToken cancellationToken);

        // must be called whenever the matching content is evicted from IDiskCache
        UniTask RemoveAsync(string diskKey, CancellationToken cancellationToken);

        // used by budget-based eviction sweeps
        UniTask<IReadOnlyList<(string Key, CacheEntryMetadata Metadata)>> GetAllAsync(CancellationToken cancellationToken);
    }
}
