using System;
using NUnit.Framework;

namespace SiPV.AssetLoader.Tests
{
    // Lifetime edge cases for RamCache + AssetHandle - the case study's leak/perf-sensitive area.
    // Plain NUnit [Test], all synchronous - RamCache itself has no async surface.
    public class RamCacheTests
    {
        private static CacheEntryMetadata Metadata() =>
            new CacheEntryMetadata("etag", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "text/fake");

        [Test]
        public void Miss_ReturnsFalse()
        {
            var cache = new RamCache(new FakeMemorySizeEstimator(), new FakeAssetReleaser());

            var found = cache.TryGet<FakeAsset>("missing", out var handle);

            Assert.IsFalse(found);
            Assert.IsNull(handle);
        }

        [Test]
        public void PutThenGet_HitReturnsIndependentHandle()
        {
            var cache = new RamCache(new FakeMemorySizeEstimator(), new FakeAssetReleaser());
            var asset = new FakeAsset("hello");

            using var first = cache.Put("key-a", asset, Metadata());
            var found = cache.TryGet<FakeAsset>("key-a", out var second);

            Assert.IsTrue(found);
            Assert.AreEqual("hello", second.Asset.Content);
            Assert.AreEqual(2, first.RefCount, "a hit bumps the shared ref count");

            second.Release();
        }

        [Test]
        public void RefCountAboveZero_PreventsDestroyOnEvict()
        {
            var releaser = new FakeAssetReleaser();
            var cache = new RamCache(new FakeMemorySizeEstimator(), releaser);
            var asset = new FakeAsset("still in use");
            var handle = cache.Put("key-b", asset, Metadata());

            cache.Evict("key-b");

            Assert.IsEmpty(releaser.Released, "must not destroy while a live handle still references it");

            handle.Release();
            Assert.AreEqual(1, releaser.Released.Count, "releasing the last handle after eviction must destroy it");
            Assert.AreSame(asset, releaser.Released[0]);
        }

        [Test]
        public void DeferredDestroy_OnlyFiresOnLastRelease()
        {
            var releaser = new FakeAssetReleaser();
            var cache = new RamCache(new FakeMemorySizeEstimator(), releaser);
            var asset = new FakeAsset("shared");
            var first = cache.Put("key-c", asset, Metadata());
            var second = first.Retain();

            cache.Evict("key-c");
            first.Release();
            Assert.IsEmpty(releaser.Released, "one of two live handles releasing must not destroy yet");

            second.Release();
            Assert.AreEqual(1, releaser.Released.Count, "the last of two live handles releasing must destroy");
        }

        [Test]
        public void ZeroRefEntry_StaysCachedAndReusable_UntilTrimmed()
        {
            var releaser = new FakeAssetReleaser();
            var cache = new RamCache(new FakeMemorySizeEstimator(), releaser);
            var asset = new FakeAsset("reusable");
            var handle = cache.Put("key-d", asset, Metadata());

            handle.Release(); // ref count now 0, but still cached - not destroyed
            Assert.IsEmpty(releaser.Released);

            var found = cache.TryGet<FakeAsset>("key-d", out var reused);
            Assert.IsTrue(found, "a zero-ref entry not yet trimmed must still be a cache hit");
            Assert.AreEqual("reusable", reused.Asset.Content);
            Assert.AreEqual(1, reused.RefCount, "reuse starts a fresh generation at ref count 1, not 2");

            reused.Release();
        }

        [Test]
        public void Put_OverwritesZeroRefEntry_DestroysOldAsset()
        {
            var releaser = new FakeAssetReleaser();
            var cache = new RamCache(new FakeMemorySizeEstimator(), releaser);
            var oldAsset = new FakeAsset("old");
            var newAsset = new FakeAsset("new");

            cache.Put("key-h", oldAsset, Metadata()).Release(); // zero-ref, still cached
            var newHandle = cache.Put("key-h", newAsset, Metadata()); // reload for the same key

            Assert.AreEqual(1, releaser.Released.Count, "the superseded zero-ref entry must be destroyed, not orphaned");
            Assert.AreSame(oldAsset, releaser.Released[0]);
            Assert.AreEqual("new", newHandle.Asset.Content);

            newHandle.Release();
        }

        [Test]
        public void Put_OverwritesLiveEntry_DestroysOldAssetOnlyAfterLastHandleReleases()
        {
            var releaser = new FakeAssetReleaser();
            var cache = new RamCache(new FakeMemorySizeEstimator(), releaser);
            var oldAsset = new FakeAsset("old");
            var newAsset = new FakeAsset("new");

            var oldHandle = cache.Put("key-i", oldAsset, Metadata()); // not released - still live
            var newHandle = cache.Put("key-i", newAsset, Metadata()); // reload while old is still held

            Assert.IsEmpty(releaser.Released, "the old entry must survive while its handle is still live");

            oldHandle.Release();
            Assert.AreEqual(1, releaser.Released.Count, "releasing the orphaned old handle must destroy its asset");
            Assert.AreSame(oldAsset, releaser.Released[0]);

            newHandle.Release();
        }

        [Test]
        public void TrimToBudget_ByEntryCount_EvictsOldestLastAccessFirst()
        {
            var releaser = new FakeAssetReleaser();
            var cache = new RamCache(new FakeMemorySizeEstimator(), releaser, maxEntries: 2);

            var oldest = new FakeAsset("oldest");
            var middle = new FakeAsset("middle");
            var newest = new FakeAsset("newest");

            cache.Put("old", oldest, Metadata()).Release();
            cache.Put("mid", middle, Metadata()).Release();
            // both zero-ref, 2 entries, at budget - no trim needed yet

            cache.Put("new", newest, Metadata()).Release(); // 3rd entry pushes over budget of 2

            Assert.AreEqual(1, releaser.Released.Count, "exactly one entry must be evicted to get back to budget");
            Assert.AreSame(oldest, releaser.Released[0], "the least-recently-accessed entry must go first");

            Assert.IsFalse(cache.TryGet<FakeAsset>("old", out _));
            Assert.IsTrue(cache.TryGet<FakeAsset>("mid", out var midHandle));
            midHandle.Release();
            Assert.IsTrue(cache.TryGet<FakeAsset>("new", out var newHandle));
            newHandle.Release();
        }

        [Test]
        public void TrimToBudget_ByEstimatedBytes_EvictsUntilUnderBudget()
        {
            var releaser = new FakeAssetReleaser();
            var estimator = new FakeMemorySizeEstimator { Estimate = _ => 5 };
            var cache = new RamCache(estimator, releaser, maxBytes: 10);

            cache.Put("a", new FakeAsset("a"), Metadata()).Release();
            cache.Put("b", new FakeAsset("b"), Metadata()).Release();
            // 10 bytes total, == budget, no trim yet

            cache.Put("c", new FakeAsset("c"), Metadata()).Release(); // 15 bytes, over budget

            Assert.AreEqual(1, releaser.Released.Count, "must evict exactly enough to get back under the byte budget");
        }

        [Test]
        public void LiveHandle_NeverSelectedForBudgetTrim()
        {
            var releaser = new FakeAssetReleaser();
            var cache = new RamCache(new FakeMemorySizeEstimator(), releaser, maxEntries: 1);

            var stillHeld = cache.Put("held", new FakeAsset("held"), Metadata());
            // don't release "held" - it must survive the next Put even though maxEntries is 1

            cache.Put("other", new FakeAsset("other"), Metadata()).Release();

            Assert.IsTrue(stillHeld.IsValid, "a referenced entry must never be evicted by budget pressure");
            stillHeld.Release();
        }

        [Test]
        public void DoubleRelease_LogsWarning_DoesNotThrow()
        {
            var logger = new FakeAssetLoaderLogger();
            var cache = new RamCache(new FakeMemorySizeEstimator(), new FakeAssetReleaser(), logger: logger);
            var handle = cache.Put("key-e", new FakeAsset("x"), Metadata());

            handle.Release();
            Assert.DoesNotThrow(() => handle.Release());

            Assert.AreEqual(1, logger.Warnings.Count, "a second release by the same instance must log a warning");
        }

        [Test]
        public void AccessAfterRelease_ThrowsObjectDisposedException()
        {
            var cache = new RamCache(new FakeMemorySizeEstimator(), new FakeAssetReleaser());
            var handle = cache.Put("key-f", new FakeAsset("x"), Metadata());

            handle.Release();

            Assert.Throws<ObjectDisposedException>(() => _ = handle.Asset);
        }

        [Test]
        public void RetainAfterFullRelease_Throws()
        {
            var cache = new RamCache(new FakeMemorySizeEstimator(), new FakeAssetReleaser());
            var handle = cache.Put("key-g", new FakeAsset("x"), Metadata());

            handle.Release();

            Assert.Throws<InvalidOperationException>(() => handle.Retain());
        }
    }
}
