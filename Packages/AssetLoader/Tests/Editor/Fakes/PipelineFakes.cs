using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader.Tests
{
    // Test-only decode target. Real asset types (Texture2D, AudioClip) need actual UnityEngine
    // objects to decode into - this avoids that ceremony for pipeline-orchestration tests.
    internal sealed class FakeAsset
    {
        public string Content { get; }
        public FakeAsset(string content) => Content = content;
    }

    internal sealed class FakeAssetDecoder : IAssetDecoder<FakeAsset>
    {
        public const string ContentType = "text/fake";

        public bool CanDecode(Type targetType, string contentTypeOrExtension) =>
            targetType == typeof(FakeAsset) && (contentTypeOrExtension == ContentType || contentTypeOrExtension == "fake");

        public UniTask<FakeAsset> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken) =>
            UniTask.FromResult(new FakeAsset(Encoding.UTF8.GetString(processedBytes)));
    }

    internal sealed class FakeCacheKeyProvider : ICacheKeyProvider
    {
        public string GetRamKey(in AssetRequest request) => request.ResolveCacheSeed();
        public string GetDiskKey(in AssetRequest request) => request.ResolveCacheSeed();
    }

    // Mirrors the real RamCache contract (ref-counted Put/TryGet) without any eviction policy -
    // pipeline tests care about call shape, not budget/LRU behavior (that's Step 06's job).
    internal sealed class FakeRamCache : IRamCache
    {
        private readonly Dictionary<string, object> _entries = new Dictionary<string, object>();
        public int EvictCallCount;
        public int TrimCallCount;

        // set on every TryGet/Put call so tests can assert the main-thread-only contract held
        public int? LastCallThreadId;

        public bool TryGet<T>(string ramKey, out AssetHandle<T> handle)
        {
            LastCallThreadId = Thread.CurrentThread.ManagedThreadId;

            if (_entries.TryGetValue(ramKey, out var boxed) && boxed is AssetHandle<T> existing)
            {
                handle = existing.Retain();
                return true;
            }

            handle = null;
            return false;
        }

        public AssetHandle<T> Put<T>(string ramKey, T asset, CacheEntryMetadata metadata)
        {
            LastCallThreadId = Thread.CurrentThread.ManagedThreadId;
            var handle = new AssetHandle<T>(ramKey, asset, key => _entries.Remove(key));
            _entries[ramKey] = handle;
            return handle;
        }

        public void Evict(string ramKey)
        {
            EvictCallCount++;
            _entries.Remove(ramKey);
        }

        public void TrimToBudget() => TrimCallCount++;

        public bool Contains(string ramKey) => _entries.ContainsKey(ramKey);
    }

    internal sealed class FakeDiskCache : IDiskCache
    {
        private readonly Dictionary<string, byte[]> _store = new Dictionary<string, byte[]>();
        public int ReadCallCount;
        public int WriteCallCount;

        public void Seed(string diskKey, byte[] content) => _store[diskKey] = content;

        public UniTask<DiskCacheReadResult> TryReadAsync(string diskKey, CancellationToken cancellationToken)
        {
            ReadCallCount++;
            return UniTask.FromResult(_store.TryGetValue(diskKey, out var bytes)
                ? new DiskCacheReadResult(true, bytes)
                : DiskCacheReadResult.Miss);
        }

        public UniTask WriteAsync(string diskKey, byte[] content, CancellationToken cancellationToken)
        {
            WriteCallCount++;
            _store[diskKey] = content;
            return UniTask.CompletedTask;
        }

        public UniTask EvictAsync(string diskKey, CancellationToken cancellationToken)
        {
            _store.Remove(diskKey);
            return UniTask.CompletedTask;
        }

        public UniTask<long> GetTotalSizeBytesAsync(CancellationToken cancellationToken)
        {
            long total = 0;
            foreach (var entry in _store)
            {
                total += entry.Value.Length;
            }

            return UniTask.FromResult(total);
        }

        public UniTask TrimToBudgetAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;
    }

    internal sealed class FakeDiskCacheMetadataStore : IDiskCacheMetadataStore
    {
        private readonly Dictionary<string, CacheEntryMetadata> _store = new Dictionary<string, CacheEntryMetadata>();
        public int SetCallCount;

        public void Seed(string diskKey, CacheEntryMetadata metadata) => _store[diskKey] = metadata;

        public bool Contains(string diskKey) => _store.ContainsKey(diskKey);

        public UniTask<CacheEntryMetadata?> GetAsync(string diskKey, CancellationToken cancellationToken) =>
            UniTask.FromResult(_store.TryGetValue(diskKey, out var metadata) ? (CacheEntryMetadata?)metadata : null);

        public UniTask SetAsync(string diskKey, CacheEntryMetadata metadata, CancellationToken cancellationToken)
        {
            SetCallCount++;
            _store[diskKey] = metadata;
            return UniTask.CompletedTask;
        }

        public UniTask RemoveAsync(string diskKey, CancellationToken cancellationToken)
        {
            _store.Remove(diskKey);
            return UniTask.CompletedTask;
        }

        public UniTask<IReadOnlyList<(string Key, CacheEntryMetadata Metadata)>> GetAllAsync(CancellationToken cancellationToken)
        {
            var list = new List<(string, CacheEntryMetadata)>();
            foreach (var entry in _store)
            {
                list.Add((entry.Key, entry.Value));
            }

            return UniTask.FromResult((IReadOnlyList<(string, CacheEntryMetadata)>)list);
        }
    }

    internal sealed class FakeCachePolicy : ICachePolicy
    {
        // default: no metadata = Miss, otherwise Fresh. Override per test for stale/miss scenarios.
        public Func<CacheEntryMetadata?, CacheLookupResult> Resolve =
            metadata => metadata.HasValue ? CacheLookupResult.Fresh : CacheLookupResult.Miss;

        public CacheLookupResult Evaluate(in AssetRequest request, CacheEntryMetadata? existingMetadata, DateTimeOffset nowUtc) =>
            Resolve(existingMetadata);
    }

    internal sealed class FakeRetryPolicy : IRetryPolicy
    {
        // default: never retry, surface the first failure immediately
        public Func<RetryContext, RetryDecision> Decide = _ => RetryDecision.Stop();

        public RetryDecision ShouldRetry(in RetryContext context) => Decide(context);
    }

    internal sealed class FakeTimeoutPolicy : ITimeoutPolicy
    {
        // short default - if a test ever gets stuck waiting on a real timeout instead of resolving
        // via its fakes, it should fail fast, not burn tens of real seconds first
        public TimeSpan Timeout = TimeSpan.FromSeconds(2);
        public TimeSpan? OverallDeadline;

        public TimeSpan GetTimeout(in AssetRequest request) => Timeout;
        public TimeSpan? GetOverallDeadline(in AssetRequest request) => OverallDeadline;
    }

    // Blocks FetchAsync until the test releases the gate (or the caller's token cancels), for
    // deterministic mid-flight cancellation tests without relying on wall-clock timing.
    internal sealed class FakeAssetSource : IAssetSource
    {
        public int FetchCallCount;
        public Func<AssetSourceRequestContext, AssetSourceResult> ResultFactory =
            _ => throw new InvalidOperationException("FakeAssetSource.ResultFactory not configured for this test.");

        // set on every FetchAsync call so tests can assert the main-thread-only contract held
        public int? LastCallThreadId;

        private UniTaskCompletionSource _gate;

        public void ArmGate() => _gate = new UniTaskCompletionSource();
        public void ReleaseGate() => _gate?.TrySetResult();

        public async UniTask<AssetSourceResult> FetchAsync(AssetSourceRequestContext context, CancellationToken cancellationToken)
        {
            FetchCallCount++;
            LastCallThreadId = Thread.CurrentThread.ManagedThreadId;

            // Consume-and-clear: a UniTaskCompletionSource is single-use, and ArmGate() only ever
            // arms the *next* call. Without clearing here, a second FetchAsync later in the same
            // test (e.g. a recovery load after a cancelled one) would await this same
            // already-completed gate again and never resolve normally - stuck until whatever
            // unrelated timeout that second call happens to have finally fires for real.
            var gate = _gate;
            _gate = null;

            if (gate != null)
            {
                using (cancellationToken.Register(() => gate.TrySetCanceled()))
                {
                    await gate.Task;
                }
            }

            return ResultFactory(context);
        }
    }

    internal sealed class RecordingEventSink : IAssetLoaderEventSink
    {
        public readonly List<AssetLoaderEventKind> Events = new List<AssetLoaderEventKind>();
        public void Report(in AssetLoaderEvent loaderEvent) => Events.Add(loaderEvent.Kind);
    }

    // Wires a full AssetLoaderConfig from the fakes above so tests only deal with the pieces
    // relevant to what they're asserting.
    internal sealed class PipelineTestHarness
    {
        public readonly FakeAssetSource Source = new FakeAssetSource();
        public readonly FakeRamCache RamCache = new FakeRamCache();
        public readonly FakeDiskCache DiskCache = new FakeDiskCache();
        public readonly FakeDiskCacheMetadataStore MetadataStore = new FakeDiskCacheMetadataStore();
        public readonly FakeCacheKeyProvider CacheKeyProvider = new FakeCacheKeyProvider();
        public readonly FakeCachePolicy CachePolicy = new FakeCachePolicy();
        public readonly FakeRetryPolicy RetryPolicy = new FakeRetryPolicy();
        public readonly FakeTimeoutPolicy TimeoutPolicy = new FakeTimeoutPolicy();
        public readonly RecordingEventSink EventSink = new RecordingEventSink();
        public readonly AssetLoaderService Loader;

        public PipelineTestHarness(bool registerDecoder = true)
        {
            var builder = new AssetLoaderConfigBuilder()
                .UseSource(Source)
                .UseRamCache(RamCache)
                .UseDiskCache(DiskCache, MetadataStore)
                .UseCacheKeyProvider(CacheKeyProvider)
                .UseTimeoutPolicy(TimeoutPolicy)
                .UseRetryPolicy(RetryPolicy)
                .UseCachePolicy(CachePolicy)
                .UseEventSink(EventSink);

            if (registerDecoder)
            {
                builder.RegisterDecoder<FakeAsset>(new FakeAssetDecoder());
            }

            Loader = new AssetLoaderService(builder.Build());
        }

        public static AssetSourceResult Ok200(string content, string eTag = "etag-1", TimeSpan? maxAge = null) =>
            new AssetSourceResult(
                AssetSourceStatus.Ok200, Encoding.UTF8.GetBytes(content), eTag, maxAge, DateTimeOffset.UtcNow, FakeAssetDecoder.ContentType);

        public static AssetSourceResult NotModified304() =>
            new AssetSourceResult(AssetSourceStatus.NotModified304, null, null, null, DateTimeOffset.UtcNow, null);
    }
}
