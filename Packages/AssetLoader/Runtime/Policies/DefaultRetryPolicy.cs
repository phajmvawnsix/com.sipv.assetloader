using System;

namespace SiPV.AssetLoader
{
    /// <summary>Default <see cref="IRetryPolicy"/>: exponential backoff with jitter, fetch failures only.</summary>
    /// <remarks>
    /// Only retries <see cref="RetryStage.Fetch"/>: a processing or decode failure means the bytes
    /// downloaded fine and the failure is deterministic, so retrying would just fail the same way
    /// again. A fetch failure is retried when it is a timeout or a 5xx-or-unknown-status error;
    /// 4xx client errors are treated as non-retryable.
    /// </remarks>
    public sealed class DefaultRetryPolicy : IRetryPolicy
    {
        private readonly int _maxAttempts;
        private readonly TimeSpan _baseDelay;
        private readonly TimeSpan _maxDelay;
        private readonly double _jitterFraction;
        private readonly Random _random = new Random();

        /// <summary>Creates a retry policy with the given attempt count and backoff shape.</summary>
        /// <param name="maxAttempts">Total attempts allowed, including the first. Defaults to 3.</param>
        /// <param name="baseDelay">Delay before the first retry. Defaults to 500ms, doubling each subsequent attempt.</param>
        /// <param name="maxDelay">Upper bound the exponential backoff is capped at. Defaults to 10 seconds.</param>
        /// <param name="jitterFraction">Fraction of the capped delay added as random jitter, to avoid retry storms. Defaults to 0.2.</param>
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

        /// <inheritdoc />
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
