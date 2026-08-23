namespace SiPV.AssetLoader
{
    /// <summary>
    /// In-memory tier holding decoded, ref-counted assets. The hottest path in the package.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Synchronous and main-thread-only by contract, deliberately unlike <see cref="IDiskCache"/>:
    /// a cache hit must not pay for an async state machine, and the assets it holds are Unity
    /// objects that can only be touched from the main thread anyway. Do not add locking to a custom
    /// implementation; call it from the main thread instead.
    /// </para>
    /// <para>
    /// Ref-counting lives here. Eviction only ever destroys entries nothing holds a live handle to,
    /// so this is an LRU among unreferenced entries rather than a true global LRU: a pinned asset
    /// is never destroyed out from under its owner, even under budget pressure.
    /// </para>
    /// </remarks>
    public interface IRamCache
    {
        /// <summary>Looks up a cached asset and takes a reference on it.</summary>
        /// <typeparam name="T">Expected asset type. A key cached under a different type is a miss.</typeparam>
        /// <param name="ramKey">Cache key, from <see cref="ICacheKeyProvider.GetRamKey"/>.</param>
        /// <param name="handle">The new handle on a hit, null on a miss.</param>
        /// <returns>True on a hit.</returns>
        /// <remarks>
        /// A hit bumps the ref count, so every successful call hands out a handle the caller must
        /// release. Implementations should also refresh the entry's last-access time here.
        /// </remarks>
        bool TryGet<T>(string ramKey, out AssetHandle<T> handle);

        /// <summary>Stores a freshly decoded asset and returns the caller's handle to it.</summary>
        /// <typeparam name="T">Asset type.</typeparam>
        /// <param name="ramKey">Cache key to store under.</param>
        /// <param name="asset">The decoded asset.</param>
        /// <param name="metadata">Freshness and size metadata for budget accounting.</param>
        /// <returns>A handle at ref count 1, already owned by the caller.</returns>
        /// <remarks>
        /// Returns the handle directly rather than expecting a follow-up <see cref="TryGet{T}"/>,
        /// which would take a second reference and leak one per load. Storing over a key that
        /// already holds an unreferenced entry must destroy the old asset first, otherwise it is
        /// orphaned with no way to ever reclaim it.
        /// </remarks>
        AssetHandle<T> Put<T>(string ramKey, T asset, CacheEntryMetadata metadata);

        /// <summary>Removes an entry unconditionally, ignoring its ref count.</summary>
        /// <param name="ramKey">Cache key to drop.</param>
        /// <remarks>
        /// For explicit invalidation, not routine eviction. An entry with live handles is removed
        /// from the lookup so nothing new resolves to it, but the asset itself stays alive until
        /// its last handle is released, so existing holders are unaffected.
        /// </remarks>
        void Evict(string ramKey);

        /// <summary>
        /// Evicts unreferenced entries, least-recently-used first, until the cache is back inside
        /// its configured budget. Entries with live handles are skipped.
        /// </summary>
        void TrimToBudget();
    }
}
