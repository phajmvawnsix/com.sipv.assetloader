using System;

namespace SiPV.AssetLoader
{
    // Default IRetryPolicy. Only retries RetryStage.Fetch.
    public sealed class DefaultRetryPolicy : IRetryPolicy
    {
        private readonly int _maxAttempts;
        private readonly TimeSpan _baseDelay;
        private readonly TimeSpan _maxDelay;
        private readonly double _jitterFraction;
        private readonly Random _random = new Random();

        public DefaultRetryPolicy(
            int maxAttempts = 3,
            TimeSpan? baseDelay = null,
            TimeSpan? maxDelay = null,
            double jitterFraction = 0.2)
        {
            _maxAttempts = maxAttempts;
            _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(500);
            _maxDelay = maxDelay ?? TimeSpan.FromSeconds(10);
            _jitterFraction = jitterFraction;
        }

        public RetryDecision ShouldRetry(in RetryContext context)
        {
            if (context.Stage != RetryStage.Fetch)
            {
                return RetryDecision.Stop();
            }

            if (context.AttemptNumber >= _maxAttempts)
            {
                return RetryDecision.Stop();
            }

            if (!IsRetryable(context.LastException))
            {
                return RetryDecision.Stop();
            }

            return RetryDecision.Retry(BackoffDelay(context.AttemptNumber));
        }

        private static bool IsRetryable(Exception failure)
        {
            if (!(failure is AssetLoadException loadException))
            {
                return false;
            }

            if (loadException.ErrorCode == AssetLoadErrorCode.TimedOut)
            {
                return true;
            }

            if (loadException.ErrorCode != AssetLoadErrorCode.FetchFailed)
            {
                return false;
            }
            
            return !loadException.HttpStatusCode.HasValue || loadException.HttpStatusCode.Value >= 500;
        }

        private TimeSpan BackoffDelay(int attemptNumber)
        {
            var exponential = _baseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1);
            var capped = Math.Min(exponential, _maxDelay.TotalMilliseconds);
            var jitter = capped * _jitterFraction * _random.NextDouble();

            return TimeSpan.FromMilliseconds(capped + jitter);
        }
    }
}
