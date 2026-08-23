using System;
using System.Collections;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace SiPV.AssetLoader.Tests
{
    // Orchestration tests against AssetLoaderService with fakes for every IO boundary
    // (source/RAM/disk/metadata/policies) - no real network, filesystem, or decoders yet.
    //
    // [UnityTest] + ToCoroutine() instead of plain [Test] async Task: this project's bundled
    // NUnit doesn't run async Task test methods (throws "Method has non-void return value, but
    // no result is expected" on every one) - the coroutine bridge is UniTask's documented
    // workaround and doesn't depend on NUnit's own async support at all.
    public class AssetLoadPipelineTests
    {
        [UnityTest]
        public IEnumerator RamCacheHit_SkipsDiskAndSource() => RunAsync(async () =>
        {
            var harness = new PipelineTestHarness();
            harness.Source.ResultFactory = _ => PipelineTestHarness.Ok200("hello");
            var request = new AssetRequest("http://example.com/a.fake");

            using var first = await harness.Loader.LoadAsync<FakeAsset>(request);
            Assert.AreEqual(1, harness.Source.FetchCallCount);
            Assert.AreEqual(1, harness.DiskCache.WriteCallCount);

            using var second = await harness.Loader.LoadAsync<FakeAsset>(request);

            Assert.AreEqual(1, harness.Source.FetchCallCount, "second load should be a pure RAM hit");
            Assert.AreEqual(0, harness.DiskCache.ReadCallCount, "RAM hit must not touch disk cache at all");
            Assert.AreEqual("hello", second.Asset.Content);
            CollectionAssert.Contains(harness.EventSink.Events, AssetLoaderEventKind.RamCacheHit);
        });

        [UnityTest]
        public IEnumerator DiskCacheHit_Fresh_SkipsSource() => RunAsync(async () =>
        {
            var harness = new PipelineTestHarness();
            var request = new AssetRequest("http://example.com/b.fake");
            var diskKey = harness.CacheKeyProvider.GetDiskKey(request);
            var metadata = new CacheEntryMetadata(
                "etag-1", TimeSpan.FromMinutes(5), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 6, FakeAssetDecoder.ContentType);

            harness.DiskCache.Seed(diskKey, Encoding.UTF8.GetBytes("cached"));
            harness.MetadataStore.Seed(diskKey, metadata);
            harness.CachePolicy.Resolve = _ => CacheLookupResult.Fresh;

            using var handle = await harness.Loader.LoadAsync<FakeAsset>(request);

            Assert.AreEqual(0, harness.Source.FetchCallCount, "fresh disk hit must not call the source");
            Assert.AreEqual(1, harness.DiskCache.ReadCallCount);
            Assert.AreEqual("cached", handle.Asset.Content);
            Assert.AreEqual(1, harness.MetadataStore.SetCallCount, "a fresh hit must still refresh LastAccessUtc for LRU");
            CollectionAssert.Contains(harness.EventSink.Events, AssetLoaderEventKind.DiskCacheHit);
        });

        [UnityTest]
        public IEnumerator Revalidation_WithNoKnownMetadata_ThrowsInsteadOfCrashing() => RunAsync(async () =>
        {
            // BypassDiskCache skips the metadata read entirely, so existingMetadata stays null.
            // A source that still answers 304 in that state (its own caching, or a bug) must not
            // NRE the pipeline - it should surface as a normal load failure.
            var harness = new PipelineTestHarness();
            harness.Source.ResultFactory = _ => PipelineTestHarness.NotModified304();
            var request = new AssetRequest("http://example.com/h.fake", flags: AssetRequestFlags.BypassDiskCache);

            var ex = await CatchAsync<AssetLoadException>(async () => await harness.Loader.LoadAsync<FakeAsset>(request));

            Assert.AreEqual(AssetLoadErrorCode.FetchFailed, ex.ErrorCode);
        });

        [UnityTest]
        public IEnumerator RamCacheAccess_HappensOnMainThread() => RunAsync(async () =>
        {
            var harness = new PipelineTestHarness();
            harness.Source.ResultFactory = _ => PipelineTestHarness.Ok200("threaded");
            var request = new AssetRequest("http://example.com/i.fake");
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;

            using var handle = await harness.Loader.LoadAsync<FakeAsset>(request);

            Assert.AreEqual(mainThreadId, harness.RamCache.LastCallThreadId, "RamCache.Put must run on the main thread");
            Assert.AreEqual(mainThreadId, harness.Source.LastCallThreadId, "IAssetSource.FetchAsync must run on the main thread");
        });

        [UnityTest]
        public IEnumerator DiskCacheHit_Stale_RevalidatesViaSource() => RunAsync(async () =>
        {
            var harness = new PipelineTestHarness();
            var request = new AssetRequest("http://example.com/c.fake");
            var diskKey = harness.CacheKeyProvider.GetDiskKey(request);
            var metadata = new CacheEntryMetadata(
                "etag-1", TimeSpan.FromSeconds(1), DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-1), 6, FakeAssetDecoder.ContentType);

            harness.DiskCache.Seed(diskKey, Encoding.UTF8.GetBytes("stale!"));
            harness.MetadataStore.Seed(diskKey, metadata);
            harness.CachePolicy.Resolve = _ => CacheLookupResult.StaleRevalidate;
            harness.Source.ResultFactory = context =>
            {
                Assert.AreEqual("etag-1", context.ETagIfKnown, "revalidation must send the known ETag");
                return PipelineTestHarness.NotModified304();
            };

            using var handle = await harness.Loader.LoadAsync<FakeAsset>(request);

            Assert.AreEqual(1, harness.Source.FetchCallCount);
            Assert.AreEqual(0, harness.DiskCache.WriteCallCount, "304 must reuse existing bytes, not rewrite them");
            Assert.AreEqual("stale!", handle.Asset.Content);
            Assert.GreaterOrEqual(harness.MetadataStore.SetCallCount, 1, "revalidation must refresh stored metadata");
            CollectionAssert.Contains(harness.EventSink.Events, AssetLoaderEventKind.DiskCacheRevalidated);
        });

        [UnityTest]
        public IEnumerator CacheMiss_FullChain_PopulatesBothTiers() => RunAsync(async () =>
        {
            var harness = new PipelineTestHarness();
            var request = new AssetRequest("http://example.com/d.fake");
            harness.Source.ResultFactory = _ => PipelineTestHarness.Ok200("fresh-bytes", "etag-2", TimeSpan.FromMinutes(10));

            using var handle = await harness.Loader.LoadAsync<FakeAsset>(request);

            Assert.AreEqual(1, harness.Source.FetchCallCount);
            Assert.AreEqual(1, harness.DiskCache.WriteCallCount);
            Assert.AreEqual(1, harness.MetadataStore.SetCallCount);
            Assert.IsTrue(harness.RamCache.Contains(harness.CacheKeyProvider.GetRamKey(request)));
            Assert.AreEqual("fresh-bytes", handle.Asset.Content);
            CollectionAssert.Contains(harness.EventSink.Events, AssetLoaderEventKind.CacheMiss);
        });

        [UnityTest]
        public IEnumerator DecoderNotRegistered_ThrowsClearError() => RunAsync(async () =>
        {
            var harness = new PipelineTestHarness(registerDecoder: false);
            harness.Source.ResultFactory = _ => PipelineTestHarness.Ok200("no decoder for this");
            var request = new AssetRequest("http://example.com/e.fake");

            var ex = await CatchAsync<AssetLoadException>(async () => await harness.Loader.LoadAsync<FakeAsset>(request));

            Assert.AreEqual(AssetLoadErrorCode.DecodeFailed, ex.ErrorCode);
            StringAssert.Contains(nameof(FakeAsset), ex.Message);
        });

        [UnityTest]
        public IEnumerator Cancellation_MidFlight_CancelsCleanly_NoCachePollution() => RunAsync(async () =>
        {
            var harness = new PipelineTestHarness();
            harness.Source.ArmGate();
            var request = new AssetRequest("http://example.com/f.fake");
            using var cts = new CancellationTokenSource();

            var loadTask = harness.Loader.LoadAsync<FakeAsset>(request, cts.Token);
            cts.Cancel();

            await CatchAsync<OperationCanceledException>(async () => await loadTask);

            Assert.AreEqual(0, harness.DiskCache.WriteCallCount, "cancelled load must not write to disk");
            Assert.IsFalse(
                harness.RamCache.Contains(harness.CacheKeyProvider.GetRamKey(request)),
                "cancelled load must not populate the RAM cache");

            // coordinator must have cleaned up the wedged in-flight entry in its finally block -
            // prove it by loading the same key again and expecting a normal, un-stuck fetch
            harness.Source.ResultFactory = _ => PipelineTestHarness.Ok200("recovered");
            using var handle = await harness.Loader.LoadAsync<FakeAsset>(request);
            Assert.AreEqual("recovered", handle.Asset.Content);
        });

        [UnityTest]
        public IEnumerator ConcurrentDuplicateRequests_CoalesceIntoOneFetch() => RunAsync(async () =>
        {
            var harness = new PipelineTestHarness();
            harness.Source.ArmGate();
            harness.Source.ResultFactory = _ => PipelineTestHarness.Ok200("shared");
            var request = new AssetRequest("http://example.com/g.fake");

            var taskA = harness.Loader.LoadAsync<FakeAsset>(request);
            var taskB = harness.Loader.LoadAsync<FakeAsset>(request);
            harness.Source.ReleaseGate();

            using var handleA = await taskA;
            using var handleB = await taskB;

            Assert.AreEqual(1, harness.Source.FetchCallCount, "concurrent duplicate loads must share one fetch");
            Assert.AreEqual(2, handleA.RefCount, "each waiter must own an independently-releasable handle");
            CollectionAssert.Contains(harness.EventSink.Events, AssetLoaderEventKind.DedupCoalesced);

            handleA.Release();
            Assert.IsTrue(handleB.IsValid, "releasing one waiter's handle must not invalidate the other's");
            handleB.Release();
            Assert.IsFalse(handleB.IsValid);
        });

        [UnityTest]
        public IEnumerator PerRequestRetryPolicyOverride_WinsOverGlobalDefault() => RunAsync(async () =>
        {
            var harness = new PipelineTestHarness();
            harness.RetryPolicy.Decide = _ => RetryDecision.Stop(); // global: never retry

            var callCount = 0;
            harness.Source.ResultFactory = _ =>
            {
                callCount++;
                return callCount == 1
                    ? throw new AssetLoadException(AssetLoadErrorCode.FetchFailed, "key", "network error")
                    : PipelineTestHarness.Ok200("succeeded-on-retry");
            };

            var overridePolicy = new FakeRetryPolicy { Decide = _ => RetryDecision.Retry(TimeSpan.Zero) };
            var request = new AssetRequest("http://example.com/override.fake", retryPolicyOverride: overridePolicy);

            using var handle = await harness.Loader.LoadAsync<FakeAsset>(request);

            Assert.AreEqual(2, callCount, "the per-request override must retry even though the global policy never would");
            Assert.AreEqual("succeeded-on-retry", handle.Asset.Content);
        });

        private static IEnumerator RunAsync(Func<UniTask> testBody) => testBody().ToCoroutine();

        // This Unity's bundled NUnit doesn't have Assert.ThrowsAsync.
        private static async Task<TException> CatchAsync<TException>(Func<Task> action) where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException ex)
            {
                return ex;
            }

            Assert.Fail($"Expected {typeof(TException).Name} to be thrown, but nothing was.");
            return null;
        }
    }
}
