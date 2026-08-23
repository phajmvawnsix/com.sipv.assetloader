using System;
using System.Collections;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace SiPV.AssetLoader.Tests
{
    // Against a real temp folder on disk, not fakes - this is the one place actual file IO
    // correctness (atomicity, corrupt recovery, LRU eviction) has to be proven for real.
    //
    // [UnityTest] + ToCoroutine() instead of plain [Test] async Task: this project's bundled
    // NUnit doesn't run async Task test methods at all - see AssetLoadPipelineTests.cs for the
    // full explanation, same fix applied here.
    public class FileDiskCacheTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "AssetLoaderTests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator PutGet_Roundtrip_PreservesBytesAndMetadata() => RunAsync(async () =>
        {
            var metadataStore = new FileDiskCacheMetadataStore(_root);
            var diskCache = new FileDiskCache(_root, metadataStore, budgetBytes: long.MaxValue);
            var content = Encoding.UTF8.GetBytes("hello disk cache");
            var fetchedAt = DateTimeOffset.UtcNow;
            var metadata = new CacheEntryMetadata("etag-1", TimeSpan.FromMinutes(5), fetchedAt, fetchedAt, content.Length, "text/plain");

            await diskCache.WriteAsync("key-a", content, default);
            await metadataStore.SetAsync("key-a", metadata, default);

            var readResult = await diskCache.TryReadAsync("key-a", default);
            var readMetadata = await metadataStore.GetAsync("key-a", default);

            Assert.IsTrue(readResult.Found);
            CollectionAssert.AreEqual(content, readResult.Content);
            Assert.IsTrue(readMetadata.HasValue);
            Assert.AreEqual("etag-1", readMetadata.Value.ETag);
            Assert.AreEqual(metadata.IsFresh(DateTimeOffset.UtcNow), readMetadata.Value.IsFresh(DateTimeOffset.UtcNow));
        });

        [UnityTest]
        public IEnumerator Write_LeavesNoTempFileBehind() => RunAsync(async () =>
        {
            var metadataStore = new FileDiskCacheMetadataStore(_root);
            var diskCache = new FileDiskCache(_root, metadataStore, budgetBytes: long.MaxValue);

            await diskCache.WriteAsync("key-b", Encoding.UTF8.GetBytes("atomic"), default);

            var tempFiles = Directory.GetFiles(_root, "*.tmp");
            Assert.IsEmpty(tempFiles, "write must not leave a .tmp sibling behind on success");
        });

        [UnityTest]
        public IEnumerator CorruptMetadata_TreatedAsMissAndDeleted() => RunAsync(async () =>
        {
            var metadataStore = new FileDiskCacheMetadataStore(_root);
            var metadata = new CacheEntryMetadata("etag-1", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 4, "text/plain");
            await metadataStore.SetAsync("key-c", metadata, default);

            var sidecarPath = Path.Combine(_root, "key-c.json");
            File.WriteAllText(sidecarPath, "{ this is not valid json");

            var result = await metadataStore.GetAsync("key-c", default);

            Assert.IsNull(result, "corrupt metadata must read back as a miss, not throw");
            Assert.IsFalse(File.Exists(sidecarPath), "corrupt sidecar must be deleted, not left behind to fail again next time");
        });

        [UnityTest]
        public IEnumerator MissingContentFile_TreatedAsMissNotThrow() => RunAsync(async () =>
        {
            // metadata says this entry exists (e.g. disk content was cleared out from under the
            // metadata store - the exact drift risk of keeping content/metadata as separate
            // stores) - TryReadAsync must not throw just because the metadata store thinks otherwise.
            var metadataStore = new FileDiskCacheMetadataStore(_root);
            var diskCache = new FileDiskCache(_root, metadataStore, budgetBytes: long.MaxValue);
            var metadata = new CacheEntryMetadata("etag-1", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 4, "text/plain");
            await metadataStore.SetAsync("key-d", metadata, default);

            var readResult = await diskCache.TryReadAsync("key-d", default);

            Assert.IsFalse(readResult.Found);
            Assert.IsNull(readResult.Content);
        });

        [UnityTest]
        public IEnumerator TrimToBudget_EvictsLeastRecentlyAccessedFirst() => RunAsync(async () =>
        {
            var metadataStore = new FileDiskCacheMetadataStore(_root);
            var diskCache = new FileDiskCache(_root, metadataStore, budgetBytes: 10);
            var now = DateTimeOffset.UtcNow;

            await diskCache.WriteAsync("old", new byte[5], default);
            await metadataStore.SetAsync("old", new CacheEntryMetadata("e1", null, now, now.AddDays(-2), 5, "bin"), default);

            await diskCache.WriteAsync("recent", new byte[5], default);
            await metadataStore.SetAsync("recent", new CacheEntryMetadata("e2", null, now, now.AddDays(-1), 5, "bin"), default);

            // total is now 10 bytes (== budget), writing one more pushes over and must trigger a trim
            await diskCache.WriteAsync("newest", new byte[5], default);

            Assert.IsFalse((await diskCache.TryReadAsync("old", default)).Found, "oldest LastAccessUtc must be evicted first");
            Assert.IsTrue((await diskCache.TryReadAsync("recent", default)).Found, "entry within budget must survive");
            Assert.IsTrue((await diskCache.TryReadAsync("newest", default)).Found, "just-written entry must survive its own write");
            Assert.IsNull(await metadataStore.GetAsync("old", default), "evicted entry's metadata must be removed too, not just its content");
        });

        [Test]
        public void DefaultCacheKeyProvider_DiskKey_IsStableAndFilesystemSafe()
        {
            var provider = new DefaultCacheKeyProvider();
            var request = new AssetRequest("http://example.com/some path?with=query&chars#fragment");

            var keyA = provider.GetDiskKey(request);
            var keyB = provider.GetDiskKey(request);

            Assert.AreEqual(keyA, keyB, "same request must hash to the same disk key every time");
            Assert.AreEqual(64, keyA.Length, "SHA256 hex is 64 characters");
            StringAssert.IsMatch("^[0-9a-f]+$", keyA);
        }

        private static IEnumerator RunAsync(Func<UniTask> testBody) => testBody().ToCoroutine();
    }
}
