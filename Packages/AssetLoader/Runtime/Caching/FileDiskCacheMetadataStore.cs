using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SiPV.AssetLoader
{
    // Default IDiskCacheMetadataStore. One sidecar .json file per entry, same rootPath and diskKey
    // as FileDiskCache's content files (different extension) - a crash writing one entry's metadata
    // can't corrupt any other entry's, which a single shared manifest file would risk.
    //
    // Metadata JSON is tiny (a few hundred bytes), so plain synchronous File IO is used here rather
    // than FileDiskCache's manual async FileStream dance - still runs on the thread pool per the
    // interface contract, just not worth the extra ceremony for reads/writes this small.
    public sealed class FileDiskCacheMetadataStore : IDiskCacheMetadataStore
    {
        [Serializable]
        private sealed class MetadataDto
        {
            public string eTag;
            public bool hasMaxAge;
            public double maxAgeSeconds;
            public long fetchedAtUtcTicks;
            public long lastAccessUtcTicks;
            public long sizeBytes;
            public string contentType;
        }

        private readonly string _rootPath;

        public FileDiskCacheMetadataStore(string rootPath)
        {
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            Directory.CreateDirectory(_rootPath);
        }

        public UniTask<CacheEntryMetadata?> GetAsync(string diskKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = PathFor(diskKey);
            if (!File.Exists(path))
            {
                return UniTask.FromResult((CacheEntryMetadata?)null);
            }

            try
            {
                var dto = JsonUtility.FromJson<MetadataDto>(File.ReadAllText(path));
                return UniTask.FromResult((CacheEntryMetadata?)FromDto(dto));
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                // corrupt sidecar - treat as miss and drop the bad file rather than throw
                TryDelete(path);
                return UniTask.FromResult((CacheEntryMetadata?)null);
            }
        }

        // Concurrent SetAsync calls for the same diskKey aren't guarded here, same assumption as
        // FileDiskCache.WriteAsync - relies on the dedup coordinator upstream.
        public UniTask SetAsync(string diskKey, CacheEntryMetadata metadata, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = PathFor(diskKey);
            var tempPath = path + ".tmp";
            var json = JsonUtility.ToJson(ToDto(metadata));

            try
            {
                File.WriteAllText(tempPath, json);

                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, null);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                TryDelete(tempPath);
                throw;
            }

            return UniTask.CompletedTask;
        }

        public UniTask RemoveAsync(string diskKey, CancellationToken cancellationToken)
        {
            TryDelete(PathFor(diskKey));
            return UniTask.CompletedTask;
        }

        public UniTask<IReadOnlyList<(string Key, CacheEntryMetadata Metadata)>> GetAllAsync(CancellationToken cancellationToken)
        {
            var list = new List<(string, CacheEntryMetadata)>();

            foreach (var file in Directory.EnumerateFiles(_rootPath, "*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var dto = JsonUtility.FromJson<MetadataDto>(File.ReadAllText(file));
                    list.Add((Path.GetFileNameWithoutExtension(file), FromDto(dto)));
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    TryDelete(file); // corrupt - drop it from the sweep instead of throwing
                }
            }

            return UniTask.FromResult((IReadOnlyList<(string, CacheEntryMetadata)>)list);
        }

        private string PathFor(string diskKey) => Path.Combine(_rootPath, diskKey + ".json");

        private static MetadataDto ToDto(CacheEntryMetadata metadata) => new MetadataDto
        {
            eTag = metadata.ETag,
            hasMaxAge = metadata.MaxAge.HasValue,
            maxAgeSeconds = metadata.MaxAge.HasValue ? metadata.MaxAge.Value.TotalSeconds : 0,
            fetchedAtUtcTicks = metadata.FetchedAtUtc.UtcTicks,
            lastAccessUtcTicks = metadata.LastAccessUtc.UtcTicks,
            sizeBytes = metadata.SizeBytes,
            contentType = metadata.ContentType
        };

        private static CacheEntryMetadata FromDto(MetadataDto dto) => new CacheEntryMetadata(
            dto.eTag,
            dto.hasMaxAge ? (TimeSpan?)TimeSpan.FromSeconds(dto.maxAgeSeconds) : null,
            new DateTimeOffset(dto.fetchedAtUtcTicks, TimeSpan.Zero),
            new DateTimeOffset(dto.lastAccessUtcTicks, TimeSpan.Zero),
            dto.sizeBytes,
            dto.contentType);

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
                // don't throw on delete failure, not critical
            }
        }
    }
}
