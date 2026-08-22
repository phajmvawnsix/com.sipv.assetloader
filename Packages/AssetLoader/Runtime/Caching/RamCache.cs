using System;
using System.Collections.Generic;

namespace SiPV.AssetLoader
{
    // Default IRamCache. Synchronous, main-thread-only, no locks
    public sealed class RamCache : IRamCache
    {
        private sealed class CacheEntry
        {
            public object CurrentHandle;
            public Func<int> GetRefCount;
            public Func<object> CreateFreshHandle;
            public Action Destroy;
            public CacheEntryMetadata Metadata;
            public DateTimeOffset LastAccessUtc;
            public long EstimatedBytes;
        }

        private readonly Dictionary<string, CacheEntry> _entries = new Dictionary<string, CacheEntry>();
        private readonly IMemorySizeEstimator _sizeEstimator;
        private readonly IAssetReleaser _releaser;
        private readonly IAssetLoaderLogger _logger;
        private readonly int? _maxEntries;
        private readonly long? _maxBytes;

        public RamCache(
            IMemorySizeEstimator sizeEstimator,
            IAssetReleaser releaser,
            int? maxEntries = null,
            long? maxBytes = null,
            IAssetLoaderLogger logger = null)
        {
            _sizeEstimator = sizeEstimator ?? throw new ArgumentNullException(nameof(sizeEstimator));
            _releaser = releaser ?? throw new ArgumentNullException(nameof(releaser));
            _maxEntries = maxEntries;
            _maxBytes = maxBytes;
            _logger = logger;
        }

        public bool TryGet<T>(string ramKey, out AssetHandle<T> handle)
        {
            if (_entries.TryGetValue(ramKey, out var entry) && entry.CurrentHandle is AssetHandle<T> current)
            {
                entry.LastAccessUtc = DateTimeOffset.UtcNow;
                entry.Metadata = entry.Metadata.WithLastAccess(entry.LastAccessUtc);

                handle = current.IsValid ? current.Retain() : (AssetHandle<T>)entry.CreateFreshHandle();
                return true;
            }

            handle = null;
            return false;
        }

        public AssetHandle<T> Put<T>(string ramKey, T asset, CacheEntryMetadata metadata)
        {
            var entry = new CacheEntry
            {
                Metadata = metadata,
                LastAccessUtc = DateTimeOffset.UtcNow,
                EstimatedBytes = _sizeEstimator.EstimateBytes(asset),
                Destroy = () => _releaser.Release(asset)
            };

            entry.CreateFreshHandle = () =>
            {
                var fresh = new AssetHandle<T>(ramKey, asset, key => OnFullyReleased(key, entry), _logger);
                entry.CurrentHandle = fresh;
                entry.GetRefCount = () => fresh.RefCount;
                return fresh;
            };

            var handle = (AssetHandle<T>)entry.CreateFreshHandle();
            
            if (_entries.TryGetValue(ramKey, out var superseded) && superseded.GetRefCount() <= 0)
            {
                superseded.Destroy();
            }

            _entries[ramKey] = entry;
            TrimToBudget();

            return handle;
        }

        public void Evict(string ramKey)
        {
            if (!_entries.TryGetValue(ramKey, out var entry))
            {
                return;
            }

            _entries.Remove(ramKey);

            if (entry.GetRefCount() <= 0)
            {
                entry.Destroy();
            }
            // else: a live handle still exists elsewhere.
        }

        public void TrimToBudget()
        {
            if (!OverBudget())
            {
                return;
            }

            var candidates = new List<KeyValuePair<string, CacheEntry>>();
            foreach (var entry in _entries)
            {
                if (entry.Value.GetRefCount() <= 0)
                {
                    candidates.Add(entry);
                }
            }

            candidates.Sort((a, b) => a.Value.LastAccessUtc.CompareTo(b.Value.LastAccessUtc));

            foreach (var entry in candidates)
            {
                if (!OverBudget())
                {
                    break;
                }

                _entries.Remove(entry.Key);
                entry.Value.Destroy();
            }
        }

        private void OnFullyReleased(string ramKey, CacheEntry originalEntry)
        {
            if (_entries.TryGetValue(ramKey, out var current) && ReferenceEquals(current, originalEntry))
            {
                return; // still cached, sitting at zero ref - stays reusable until a future trim
            }

            // no longer reachable through the cache at all (evicted or replaced) - this was the
            // last handle for it, destroy now
            originalEntry.Destroy();
        }

        private bool OverBudget()
        {
            if (_maxEntries.HasValue && _entries.Count > _maxEntries.Value)
            {
                return true;
            }

            if (_maxBytes.HasValue && TotalEstimatedBytes() > _maxBytes.Value)
            {
                return true;
            }

            return false;
        }

        private long TotalEstimatedBytes()
        {
            long total = 0;
            foreach (var entry in _entries.Values)
            {
                total += entry.EstimatedBytes;
            }

            return total;
        }
    }
}
