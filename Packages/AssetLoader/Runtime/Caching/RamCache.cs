using System;
using System.Collections.Generic;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// In-memory asset cache with ref-counted entries and least-recently-used eviction among
    /// unreferenced ones.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Synchronous, main-thread-only, and deliberately lock-free. Confining access to the main
    /// thread is what makes locking unnecessary, and it costs nothing in practice since the assets
    /// held here are Unity objects that can only be used from the main thread anyway.
    /// </para>
    /// <para>
    /// An entry that drops to zero references is not destroyed immediately: it stays cached and
    /// reusable until an eviction sweep reclaims it, so a load-release-reload cycle still hits the
    /// cache. Reusing such an entry mints a fresh handle generation rather than resurrecting the
    /// spent one, which keeps the ref count an accurate reflection of real callers.
    /// </para>
    /// </remarks>
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

        /// <summary>Creates a RAM cache.</summary>
        /// <param name="sizeEstimator">Sizes assets for the byte budget.</param>
        /// <param name="releaser">Destroys assets once evicted.</param>
        /// <param name="maxEntries">Entry-count ceiling, or null for no limit.</param>
        /// <param name="maxBytes">Estimated-byte ceiling, or null for no limit.</param>
        /// <param name="logger">Optional, receives double-release warnings from handles this cache issues.</param>
        /// <exception cref="ArgumentNullException">Thrown when the estimator or releaser is null.</exception>
        /// <remarks>
        /// Both budgets are optional and independent: set either, both, or neither. With neither
        /// set the cache grows without bound, which is only appropriate for a fixed, known-small
        /// asset set. Budgets are targets rather than hard caps, since referenced entries are never
        /// evicted.
        /// </remarks>
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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
