using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Persists <see cref="CacheEntryMetadata"/> keyed by disk cache key, separate from the bytes
    /// in <see cref="IDiskCache"/> so either storage strategy can be swapped on its own.
    /// </summary>
    /// <remarks>
    /// Async, I/O-bound, thread-pool: like <see cref="IDiskCache"/>, implementations must not touch
    /// UnityEngine APIs. Every mutation here has a counterpart on the content store and vice versa;
    /// see the remarks on <see cref="IDiskCache"/> for what drifting apart looks like.
    /// </remarks>
    public interface IDiskCacheMetadataStore
    {
        /// <summary>Reads the metadata for a key.</summary>
        /// <param name="diskKey">Filesystem-safe key.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>The metadata, or null when nothing is recorded.</returns>
        /// <remarks>
        /// Null must also be returned for corrupt or unreadable records rather than throwing: the
        /// pipeline treats that as a miss and refetches, which is the recoverable outcome.
        /// </remarks>
        UniTask<CacheEntryMetadata?> GetAsync(string diskKey, CancellationToken cancellationToken);

        /// <summary>Writes or overwrites the metadata for a key.</summary>
        /// <param name="diskKey">Filesystem-safe key.</param>
        /// <param name="metadata">Record to persist.</param>
        /// <param name="cancellationToken">Cancels the write.</param>
        /// <remarks>
        /// Called on every write and on every read that refreshes a last-access timestamp, so it is
        /// on the hot path for repeated disk hits.
        /// </remarks>
        UniTask SetAsync(string diskKey, CacheEntryMetadata metadata, CancellationToken cancellationToken);

        /// <summary>Deletes the metadata for a key. Removing an unknown key is a no-op.</summary>
        /// <param name="diskKey">Filesystem-safe key.</param>
        /// <param name="cancellationToken">Cancels the removal.</param>
        /// <remarks>Call whenever the matching content is evicted from <see cref="IDiskCache"/>.</remarks>
        UniTask RemoveAsync(string diskKey, CancellationToken cancellationToken);

        /// <summary>Every recorded entry, for budget sweeps and cache inspection.</summary>
        /// <param name="cancellationToken">Cancels the enumeration.</param>
        /// <returns>All keys with their metadata, in no guaranteed order.</returns>
        /// <remarks>
        /// Used by eviction to sort by last-access time, and useful to a host application that
        /// wants to report or clear the whole cache.
        /// </remarks>
        UniTask<IReadOnlyList<(string Key, CacheEntryMetadata Metadata)>> GetAllAsync(CancellationToken cancellationToken);
    }
}
