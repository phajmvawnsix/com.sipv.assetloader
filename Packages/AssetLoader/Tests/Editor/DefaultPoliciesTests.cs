using System;
using NUnit.Framework;

namespace SiPV.AssetLoader.Tests
{
    public class DefaultRetryPolicyTests
    {
        private static RetryContext Context(RetryStage stage, Exception failure, int attempt = 1) =>
            new RetryContext(default, attempt, failure, stage);

        [Test]
        public void NetworkLevelFailure_NoStatusCode_IsRetryable()
        {
            var policy = new DefaultRetryPolicy();
            var failure = new AssetLoadException(AssetLoadErrorCode.FetchFailed, "key", "network error");

            var decision = policy.ShouldRetry(Context(RetryStage.Fetch, failure));

            Assert.IsTrue(decision.ShouldRetry);
        }

        [Test]
        public void ServerError_5xx_IsRetryable()
        {
            var policy = new DefaultRetryPolicy();
            var failure = new AssetLoadException(AssetLoadErrorCode.FetchFailed, "key", "HTTP 503", httpStatusCode: 503);

            var decision = policy.ShouldRetry(Context(RetryStage.Fetch, failure));

            Assert.IsTrue(decision.ShouldRetry);
        }

        [Test]
        public void ClientError_4xx_IsNotRetryable()
        {
            var policy = new DefaultRetryPolicy();
            var failure = new AssetLoadException(AssetLoadErrorCode.FetchFailed, "key", "HTTP 404", httpStatusCode: 404);

            var decision = policy.ShouldRetry(Context(RetryStage.Fetch, failure));

            Assert.IsFalse(decision.ShouldRetry);
        }

        [Test]
        public void TimedOut_IsRetryable()
        {
            var policy = new DefaultRetryPolicy();
            var failure = new AssetLoadException(AssetLoadErrorCode.TimedOut, "key", "timed out");

            var decision = policy.ShouldRetry(Context(RetryStage.Fetch, failure));

            Assert.IsTrue(decision.ShouldRetry);
        }

        [Test]
        public void ProcessOrDecodeStage_NeverRetriedByDefault()
        {
            var policy = new DefaultRetryPolicy();
            var failure = new AssetLoadException(AssetLoadErrorCode.TimedOut, "key", "would be retryable on Fetch");

            Assert.IsFalse(policy.ShouldRetry(Context(RetryStage.Process, failure)).ShouldRetry);
            Assert.IsFalse(policy.ShouldRetry(Context(RetryStage.Decode, failure)).ShouldRetry);
        }

        [Test]
        public void StopsAfterMaxAttempts()
        {
            var policy = new DefaultRetryPolicy(maxAttempts: 3);
            var failure = new AssetLoadException(AssetLoadErrorCode.FetchFailed, "key", "network error");

            Assert.IsTrue(policy.ShouldRetry(Context(RetryStage.Fetch, failure, attempt: 2)).ShouldRetry);
            Assert.IsFalse(policy.ShouldRetry(Context(RetryStage.Fetch, failure, attempt: 3)).ShouldRetry, "must stop once attempt reaches maxAttempts");
        }

        [Test]
        public void BackoffDelayGrowsWithAttemptNumber()
        {
            var policy = new DefaultRetryPolicy(maxAttempts: 10, baseDelay: TimeSpan.FromMilliseconds(100), jitterFraction: 0);
            var failure = new AssetLoadException(AssetLoadErrorCode.FetchFailed, "key", "network error");

            var first = policy.ShouldRetry(Context(RetryStage.Fetch, failure, attempt: 1));
            var second = policy.ShouldRetry(Context(RetryStage.Fetch, failure, attempt: 2));

            Assert.Greater(second.Delay, first.Delay);
        }

        [Test]
        public void NonAssetLoadException_IsNotRetryable()
        {
            var policy = new DefaultRetryPolicy();

            var decision = policy.ShouldRetry(Context(RetryStage.Fetch, new InvalidOperationException("unexpected")));

            Assert.IsFalse(decision.ShouldRetry);
        }
    }

    public class DefaultTimeoutPolicyTests
    {
        [Test]
        public void GetTimeout_ReturnsConfiguredValue()
        {
            var policy = new DefaultTimeoutPolicy(perAttemptTimeout: TimeSpan.FromSeconds(7));

            Assert.AreEqual(TimeSpan.FromSeconds(7), policy.GetTimeout(default));
        }

        [Test]
        public void GetOverallDeadline_DefaultsToNull()
        {
            var policy = new DefaultTimeoutPolicy();

            Assert.IsNull(policy.GetOverallDeadline(default));
        }

        [Test]
        public void GetOverallDeadline_ReturnsConfiguredValue()
        {
            var policy = new DefaultTimeoutPolicy(overallDeadline: TimeSpan.FromMinutes(1));

            Assert.AreEqual(TimeSpan.FromMinutes(1), policy.GetOverallDeadline(default));
        }
    }

    public class DefaultCachePolicyTests
    {
        private static AssetRequest Request(AssetRequestFlags flags = AssetRequestFlags.None) =>
            new AssetRequest("http://example.com/x.fake", flags: flags);

        [Test]
        public void NoMetadata_IsMiss()
        {
            var policy = new DefaultCachePolicy();

            var result = policy.Evaluate(Request(), null, DateTimeOffset.UtcNow);

            Assert.AreEqual(CacheLookupResult.Miss, result);
        }

        [Test]
        public void FreshMetadata_NoFlags_IsFresh()
        {
            var policy = new DefaultCachePolicy();
            var now = DateTimeOffset.UtcNow;
            var metadata = new CacheEntryMetadata("etag", TimeSpan.FromMinutes(5), now, now, 10, "text/plain");

            var result = policy.Evaluate(Request(), metadata, now);

            Assert.AreEqual(CacheLookupResult.Fresh, result);
        }

        [Test]
        public void StaleMetadata_NoFlags_IsStaleRevalidate()
        {
            var policy = new DefaultCachePolicy();
            var now = DateTimeOffset.UtcNow;
            var metadata = new CacheEntryMetadata("etag", TimeSpan.FromSeconds(1), now.AddDays(-1), now.AddDays(-1), 10, "text/plain");

            var result = policy.Evaluate(Request(), metadata, now);

            Assert.AreEqual(CacheLookupResult.StaleRevalidate, result);
        }

        [Test]
        public void ForceRefetch_IsMiss_EvenWithFreshMetadata()
        {
            var policy = new DefaultCachePolicy();
            var now = DateTimeOffset.UtcNow;
            var metadata = new CacheEntryMetadata("etag", TimeSpan.FromMinutes(5), now, now, 10, "text/plain");

            var result = policy.Evaluate(Request(AssetRequestFlags.ForceRefetch), metadata, now);

            Assert.AreEqual(CacheLookupResult.Miss, result);
        }

        [Test]
        public void ForceRevalidate_IsStaleRevalidate_EvenWithFreshMetadata()
        {
            var policy = new DefaultCachePolicy();
            var now = DateTimeOffset.UtcNow;
            var metadata = new CacheEntryMetadata("etag", TimeSpan.FromMinutes(5), now, now, 10, "text/plain");

            var result = policy.Evaluate(Request(AssetRequestFlags.ForceRevalidate), metadata, now);

            Assert.AreEqual(CacheLookupResult.StaleRevalidate, result);
        }
    }
}
