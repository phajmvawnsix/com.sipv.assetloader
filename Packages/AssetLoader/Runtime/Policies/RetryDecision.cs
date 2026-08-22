using System;

namespace SiPV.AssetLoader
{
    /// <summary>Result of IRetryPolicy.ShouldRetry.</summary>
    public readonly struct RetryDecision
    {
        public bool ShouldRetry { get; }

        public TimeSpan Delay { get; }

        private RetryDecision(bool shouldRetry, TimeSpan delay)
        {
            ShouldRetry = shouldRetry;
            Delay = delay;
        }

        // TimeSpan.Zero means retry immediately.
        public static RetryDecision Retry(TimeSpan delay) => new RetryDecision(true, delay);

        // Give up; last failure gets surfaced to the caller.
        public static RetryDecision Stop() => new RetryDecision(false, TimeSpan.Zero);
    }
}
