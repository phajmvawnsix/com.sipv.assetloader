using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// File-backed disk cache: one file per entry, at <c>&lt;rootPath&gt;/&lt;diskKey&gt;.bin</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Writes are atomic from a reader's point of view: bytes go to a temporary sibling file first
    /// and are then moved over the final path in one filesystem operation, so a crash mid-write
    /// leaves either the old content or the new one, never a truncated file.
    /// </para>
    /// <para>
    /// Read failures are misses rather than exceptions, and a file that fails to read is deleted so
    /// the next write starts clean. Losing a cache entry is always recoverable by refetching;
    /// throwing would turn a recoverable state into a failed load.
    /// </para>
    /// <para>
    /// This class deliberately never reads or writes metadata except in
    /// <see cref="TrimToBudgetAsync"/>, which needs it because the eviction order is defined as
    /// least-recently-used over the metadata store's timestamps. That is why the constructor takes
    /// a metadata store despite the two being separate concerns everywhere else.
    /// </para>
    /// </remarks>
    public sealed class FileDiskCache : IDiskCache
    {
        private const int BufferSize = 81920;

        private readonly string _rootPath;
        private readonly IDiskCacheMetadataStore _metadataStore;
        private readonly long _budgetBytes;

        /// <summary>Creates a file-backed disk cache, creating the directory if needed.</summary>
        /// <param name="rootPath">
        /// Directory for cached files. Must be writable on every target platform, which in practice
        /// means somewhere under <c>Application.persistentDataPath</c>.
        /// </param>
        /// <param name="metadataStore">
        /// The matching metadata store, used only to order budget evictions. Pass the same instance
        /// registered with <see cref="AssetLoaderConfigBuilder.UseDiskCache"/>.
        /// </param>
        /// <param name="budgetBytes">
        /// Size ceiling. Checked after each write, evicting oldest-access-first until back inside
        /// the limit.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when the path or metadata store is null.</exception>
        public FileDiskCache(string rootPath, IDiskCacheMetadataStore metadataStore, long budgetBytes)
        {
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
            _budgetBytes = budgetBytes;
            Directory.CreateDirectory(_rootPath);
        }

        /// <inheritdoc />
        public async UniTask<DiskCacheReadResult> TryReadAsync(string diskKey, CancellationToken cancellationToken)
        {
            var path = PathFor(diskKey);

            if (!File.Exists(path))
            {
                return DiskCacheReadResult.Miss;
            }

            try
            {
                using var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
                // TODO: allocates the whole file in one array. Fine for textures and audio at
                // typical sizes, but a single huge entry would be one huge allocation. Would need a
                // streaming read path if the package ever has to handle entries that large.
                var buffer = new byte[stream.Length];
                var offset = 0;

                while (offset < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken);
                    if (read == 0)
                    {
                        break; // file shrank under us mid-read - treat what we got as corrupt below
                    }

                    offset += read;
                }

                if (offset != buffer.Length)
                {
                    TryDelete(path);
                    return DiskCacheReadResult.Miss;
                }

                return new DiskCacheReadResult(true, buffer);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                TryDelete(path);
                return DiskCacheReadResult.Miss;
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Concurrent writes to the same key are not guarded here. The pipeline deduplicates
        /// concurrent loads of a key upstream, so two writers never race the same temporary path
        /// when going through the loader. Calling this class directly from several places at once
        /// would need your own coordination.
        /// </remarks>
        public async UniTask WriteAsync(string diskKey, byte[] content, CancellationToken cancellationToken)
        {
            var path = PathFor(diskKey);
            var tempPath = path + ".tmp";

            try
            {
                using (var stream = new FileStream(
                    tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
                {
                    await stream.WriteAsync(content, 0, content.Length, cancellationToken);
                }

                // File.Replace does the swap without an explicit delete-then-gap window; only
                // falls back to plain Move for the first write, when there's nothing to replace yet.
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, null);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch (OperationCanceledException)
            {
                TryDelete(tempPath);
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                TryDelete(tempPath);
                throw;
            }

            try
            {
                await TrimToBudgetAsync(cancellationToken);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                // best-effort: the write itself already succeeded, a failed trim shouldn't fault it
            }
        }

        /// <inheritdoc />
        public UniTask EvictAsync(string diskKey, CancellationToken cancellationToken)
        {
            TryDelete(PathFor(diskKey));
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public UniTask<long> GetTotalSizeBytesAsync(CancellationToken cancellationToken)
        {
            long total = 0;
            foreach (var file in Directory.EnumerateFiles(_rootPath, "*.bin"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                total += new FileInfo(file).Length;
            }

            return UniTask.FromResult(total);
        }

        /// <inheritdoc />
        public async UniTask TrimToBudgetAsync(CancellationToken cancellationToken)
        {
            var total = await GetTotalSizeBytesAsync(cancellationToken);
            if (total <= _budgetBytes)
            {
                return;
            }

            var all = await _metadataStore.GetAllAsync(cancellationToken);
            var ordered = new List<(string Key, CacheEntryMetadata Metadata)>(all);
            ordered.Sort((a, b) => a.Metadata.LastAccessUtc.CompareTo(b.Metadata.LastAccessUtc));

            foreach (var entry in ordered)
            {
                if (total <= _budgetBytes)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();

                var path = PathFor(entry.Key);
                if (File.Exists(path))
                {
                    total -= new FileInfo(path).Length;
                    TryDelete(path);
                }

                await _metadataStore.RemoveAsync(entry.Key, cancellationToken);
            }
        }

        private string PathFor(string diskKey) => Path.Combine(_rootPath, diskKey + ".bin");

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // don't throw on failed
            }
        }
    }
}
