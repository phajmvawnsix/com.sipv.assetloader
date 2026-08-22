using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Default IDiskCache. One file per entry at <rootPath>/<diskKey>.bin. Atomic write: write to a
    // .tmp sibling then File.Move over the final path, so a crash mid-write never leaves a partial
    // file at the real path. Read failures (missing or corrupt file) are a miss, not a throw - a
    // corrupt file gets deleted so the next write starts clean.
    //
    // TrimToBudgetAsync needs a metadataStore reference even though this class otherwise never
    // touches metadata (D-13 keeps content and metadata in separate stores) - the interface itself
    // documents the sweep as LRU over the metadata store's LastAccessUtc, so there's no way to
    // implement it without one. Kept scoped to that single method.
    public sealed class FileDiskCache : IDiskCache
    {
        private const int BufferSize = 81920;

        private readonly string _rootPath;
        private readonly IDiskCacheMetadataStore _metadataStore;
        private readonly long _budgetBytes;

        public FileDiskCache(string rootPath, IDiskCacheMetadataStore metadataStore, long budgetBytes)
        {
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
            _budgetBytes = budgetBytes;
            Directory.CreateDirectory(_rootPath);
        }

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
                // TODO: allocates the whole file in one array. Fine for textures/audio at typical
                // sizes, but a multi-GB entry would be a multi-GB single allocation. Revisit if
                // cache budgets (still undecided) ever allow entries that large.
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

        // Concurrent WriteAsync calls for the same diskKey aren't guarded here - relies on
        // IInFlightRequestCoordinator upstream already deduplicating concurrent loads of the same
        // key, so two writers racing the same .tmp path shouldn't happen through the pipeline.
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

        public UniTask EvictAsync(string diskKey, CancellationToken cancellationToken)
        {
            TryDelete(PathFor(diskKey));
            return UniTask.CompletedTask;
        }

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
