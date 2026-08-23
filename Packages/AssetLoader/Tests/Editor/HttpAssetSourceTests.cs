using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace SiPV.AssetLoader.Tests
{
    // HTTP semantics (ETag/Cache-Control/status mapping) against a fake transport - no real
    // UnityWebRequest, no network. See docs/step-05-http-source.md for the response mapping table.
    //
    // [UnityTest] + ToCoroutine() instead of plain [Test] async Task: this project's bundled
    // NUnit doesn't run async Task test methods at all - see AssetLoadPipelineTests.cs for the
    // full explanation, same fix applied here.
    public class HttpAssetSourceTests
    {
        private static AssetSourceRequestContext Context(string eTag = null, DateTimeOffset? lastModified = null) =>
            new AssetSourceRequestContext(
                "http://example.com/asset.bin", eTag, lastModified, null, null, AssetRequestPriority.Normal);

        [UnityTest]
        public IEnumerator Ok200_ParsesETagAndMaxAge() => RunAsync(async () =>
        {
            var httpClient = new FakeHttpClient();
            httpClient.ResponseFactory = _ => new HttpResponse(200, new byte[] { 1, 2, 3 }, new Dictionary<string, string>
            {
                ["ETag"] = "\"abc123\"",
                ["Cache-Control"] = "max-age=600",
                ["Content-Type"] = "image/png"
            });
            var source = new HttpAssetSource(httpClient);

            var result = await source.FetchAsync(Context(), default);

            Assert.AreEqual(AssetSourceStatus.Ok200, result.Status);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, result.RawBytes);
            Assert.AreEqual("\"abc123\"", result.ETag);
            Assert.AreEqual(TimeSpan.FromSeconds(600), result.MaxAge);
            Assert.AreEqual("image/png", result.ContentType);
        });

        [UnityTest]
        public IEnumerator NotModified304_HasNoBody() => RunAsync(async () =>
        {
            var httpClient = new FakeHttpClient();
            httpClient.ResponseFactory = _ => new HttpResponse(304, null, new Dictionary<string, string>());
            var source = new HttpAssetSource(httpClient);

            var result = await source.FetchAsync(Context(eTag: "\"abc123\""), default);

            Assert.AreEqual(AssetSourceStatus.NotModified304, result.Status);
            Assert.IsNull(result.RawBytes);
        });

        [UnityTest]
        public IEnumerator NotModified304_WithCacheControl_UpdatesMaxAge() => RunAsync(async () =>
        {
            // RFC 7234 4.3.4: a 304 can carry an updated Cache-Control even with no body.
            // AssetLoadPipeline reads this to refresh stored freshness on revalidation.
            var httpClient = new FakeHttpClient();
            httpClient.ResponseFactory = _ => new HttpResponse(304, null, new Dictionary<string, string>
            {
                ["Cache-Control"] = "max-age=1200"
            });
            var source = new HttpAssetSource(httpClient);

            var result = await source.FetchAsync(Context(eTag: "\"abc123\""), default);

            Assert.AreEqual(AssetSourceStatus.NotModified304, result.Status);
            Assert.AreEqual(TimeSpan.FromSeconds(1200), result.MaxAge);
        });

        [UnityTest]
        public IEnumerator NotModified304_WithNoCacheControl_LeavesMaxAgeNull() => RunAsync(async () =>
        {
            // absence of the header on a 304 means "nothing changed," not "no lifetime info" -
            // must not be reinterpreted as the 200 default of TimeSpan.Zero.
            var httpClient = new FakeHttpClient();
            httpClient.ResponseFactory = _ => new HttpResponse(304, null, new Dictionary<string, string>());
            var source = new HttpAssetSource(httpClient);

            var result = await source.FetchAsync(Context(eTag: "\"abc123\""), default);

            Assert.IsNull(result.MaxAge);
        });

        [UnityTest]
        public IEnumerator KnownETag_SendsIfNoneMatch() => RunAsync(async () =>
        {
            var httpClient = new FakeHttpClient();
            httpClient.ResponseFactory = _ => new HttpResponse(200, new byte[] { 1 }, new Dictionary<string, string>());
            var source = new HttpAssetSource(httpClient);

            await source.FetchAsync(Context(eTag: "\"abc123\""), default);

            Assert.AreEqual("\"abc123\"", httpClient.LastRequest.Value.Headers["If-None-Match"]);
        });

        [UnityTest]
        public IEnumerator NoETag_FallsBackToIfModifiedSince() => RunAsync(async () =>
        {
            var httpClient = new FakeHttpClient();
            httpClient.ResponseFactory = _ => new HttpResponse(200, new byte[] { 1 }, new Dictionary<string, string>());
            var source = new HttpAssetSource(httpClient);
            var lastModified = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            await source.FetchAsync(Context(lastModified: lastModified), default);

            Assert.IsTrue(httpClient.LastRequest.Value.Headers.ContainsKey("If-Modified-Since"));
            Assert.IsFalse(httpClient.LastRequest.Value.Headers.ContainsKey("If-None-Match"));
        });

        [UnityTest]
        public IEnumerator NoStore_NullsOutETagAndMaxAge() => RunAsync(async () =>
        {
            var httpClient = new FakeHttpClient();
            httpClient.ResponseFactory = _ => new HttpResponse(200, new byte[] { 1 }, new Dictionary<string, string>
            {
                ["ETag"] = "\"abc123\"",
                ["Cache-Control"] = "no-store"
            });
            var source = new HttpAssetSource(httpClient);

            var result = await source.FetchAsync(Context(), default);

            Assert.AreEqual(AssetSourceStatus.Ok200, result.Status, "no-store still returns the bytes, just isn't cacheable");
            Assert.IsNull(result.ETag);
            Assert.IsNull(result.MaxAge);
        });

        [UnityTest]
        public IEnumerator NoCache_AlwaysRevalidatesButKeepsETag() => RunAsync(async () =>
        {
            var httpClient = new FakeHttpClient();
            httpClient.ResponseFactory = _ => new HttpResponse(200, new byte[] { 1 }, new Dictionary<string, string>
            {
                ["ETag"] = "\"abc123\"",
                ["Cache-Control"] = "no-cache"
            });
            var source = new HttpAssetSource(httpClient);

            var result = await source.FetchAsync(Context(), default);

            Assert.AreEqual("\"abc123\"", result.ETag, "no-cache still allows storing the ETag for revalidation");
            Assert.AreEqual(TimeSpan.Zero, result.MaxAge);
        });

        [UnityTest]
        public IEnumerator MissingCacheControl_DefaultsToAlwaysRevalidate() => RunAsync(async () =>
        {
            var httpClient = new FakeHttpClient();
            httpClient.ResponseFactory = _ => new HttpResponse(200, new byte[] { 1 }, new Dictionary<string, string>());
            var source = new HttpAssetSource(httpClient);

            var result = await source.FetchAsync(Context(), default);

            Assert.AreEqual(TimeSpan.Zero, result.MaxAge);
        });

        [UnityTest]
        public IEnumerator MalformedMaxAge_FallsBackSafely() => RunAsync(async () =>
        {
            var httpClient = new FakeHttpClient();
            httpClient.ResponseFactory = _ => new HttpResponse(200, new byte[] { 1 }, new Dictionary<string, string>
            {
                ["Cache-Control"] = "max-age=not-a-number"
            });
            var source = new HttpAssetSource(httpClient);

            var result = await source.FetchAsync(Context(), default);

            Assert.AreEqual(TimeSpan.Zero, result.MaxAge);
        });

        [UnityTest]
        public IEnumerator HttpError_ReturnsFailedWithStatusCode() => RunAsync(async () =>
        {
            var httpClient = new FakeHttpClient();
            httpClient.ResponseFactory = _ => new HttpResponse(404, null, new Dictionary<string, string>());
            var source = new HttpAssetSource(httpClient);

            var result = await source.FetchAsync(Context(), default);

            Assert.AreEqual(AssetSourceStatus.Failed, result.Status);
            Assert.AreEqual(AssetLoadErrorCode.FetchFailed, result.Error.ErrorCode);
            StringAssert.Contains("404", result.Error.Message);
        });

        [UnityTest]
        public IEnumerator NetworkError_ReturnsFailed() => RunAsync(async () =>
        {
            var httpClient = new FakeHttpClient();
            httpClient.ResponseFactory = _ => HttpResponse.NetworkError("DNS failure");
            var source = new HttpAssetSource(httpClient);

            var result = await source.FetchAsync(Context(), default);

            Assert.AreEqual(AssetSourceStatus.Failed, result.Status);
            Assert.AreEqual(AssetLoadErrorCode.FetchFailed, result.Error.ErrorCode);
        });

        private static IEnumerator RunAsync(Func<UniTask> testBody) => testBody().ToCoroutine();
    }
}
