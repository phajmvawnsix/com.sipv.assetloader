using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Persistent tier storing raw bytes. Content only: freshness metadata lives in
    /// <see cref="IDiskCacheMetadataStore"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bytes are stored exactly as the source delivered them, before the content processor chain
    /// runs. An encrypted-at-rest project therefore keeps ciphertext on disk, and rotating the
    /// decryption key never invalidates the cache.
    /// </para>
    /// <para>
    /// Async and I/O-bound: implementations run on the thread pool and must not touch UnityEngine
    /// APIs. The pipeline has already left the main thread before calling in.
    /// </para>
    /// <para>
    /// Content and metadata being separate stores is what makes either swappable on its own, but it
    /// means every write and every eviction here needs the matching call on the metadata store.
    /// Miss one and the two drift apart: metadata claiming a fresh entry whose bytes are gone, or
    /// orphaned bytes no lookup can reach.
    /// </para>
    /// </remarks>
    public interface IDiskCache
    {
        /// <summary>Reads cached bytes.</summary>
        /// <param name="diskKey">Filesystem-safe key, from <see cref="ICacheKeyProvider.GetDiskKey"/>.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>A result whose Found flag is false on a miss.</returns>
        /// <remarks>A miss is a normal outcome and must not throw.</remarks>
        UniTask<DiskCacheReadResult> TryReadAsync(string diskKey, CancellationToken cancellationToken);

        /// <summary>Writes or overwrites the bytes for a key.</summary>
        /// <param name="diskKey">Filesystem-safe key.</param>
        /// <param name="content">Raw bytes, exactly as fetched.</param>
        /// <param name="cancellationToken">Cancels the write.</param>
        /// <remarks>
        /// Must be atomic from a reader's point of view: a concurrent or crash-interrupted read
        /// should see either the old content or the new one, never a half-written file.
        /// </remarks>
        UniTask WriteAsync(string diskKey, byte[] content, CancellationToken cancellationToken);

        /// <summary>Deletes the bytes for a key. Evicting a key that was never cached is a no-op.</summary>
        /// <param name="diskKey">Filesystem-safe key.</param>
        /// <param name="cancellationToken">Cancels the eviction.</param>
        /// <remarks>Pair every call with <see cref="IDiskCacheMetadataStore.RemoveAsync"/>.</remarks>
        UniTask EvictAsync(string diskKey, CancellationToken cancellationToken);

        /// <summary>Total bytes currently stored, for diagnostics and budget checks.</summary>
        /// <param name="cancellationToken">Cancels the scan.</param>
        /// <returns>Total size in bytes.</returns>
        UniTask<long> GetTotalSizeBytesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Evicts entries until total size is back inside budget, least-recently-used first
        /// according to the metadata store's last-access timestamps.
        /// </summary>
        /// <param name="cancellationToken">Cancels the trim.</param>
        UniTask TrimToBudgetAsync(CancellationToken cancellationToken);
    }
}
