namespace SiPV.AssetLoader
{
    // In-memory cache tier. Synchronous and main-thread-only on purpose: no UniTask allocation on the hot path.
    // Ref-counting lives here too. A hit bumps the count and hands back a new handle, and eviction only touches
    // zero-ref entries, so it's an LRU among unreferenced entries rather than a pure global LRU.
    public interface IRamCache
    {
        bool TryGet<T>(string ramKey, out AssetHandle<T> handle);

        // ref count starts at 1; the pipeline hands that first handle to the caller, not this method
        void Put<T>(string ramKey, T asset, CacheEntryMetadata metadata);

        // unconditional, ignores ref count - for explicit invalidation, not routine eviction
        void Evict(string ramKey);

        void TrimToBudget();
    }
}
