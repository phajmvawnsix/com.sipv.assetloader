namespace SiPV.AssetLoader
{
    // In-memory cache tier. Synchronous and main-thread-only on purpose: no UniTask allocation on the hot path.
    // Ref-counting lives here too. A hit bumps the count and hands back a new handle, and eviction only touches
    // zero-ref entries, so it's an LRU among unreferenced entries rather than a pure global LRU.
    public interface IRamCache
    {
        bool TryGet<T>(string ramKey, out AssetHandle<T> handle);

        // returns the new entry's handle at ref count 1, already owned by the caller - no follow-up
        // TryGet needed (that would double-count the ref)
        AssetHandle<T> Put<T>(string ramKey, T asset, CacheEntryMetadata metadata);

        // unconditional, ignores ref count - for explicit invalidation, not routine eviction
        void Evict(string ramKey);

        void TrimToBudget();
    }
}
