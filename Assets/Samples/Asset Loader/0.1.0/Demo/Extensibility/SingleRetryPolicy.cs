using System;
using SiPV.AssetLoader;

namespace SiPV.AssetLoader.Samples.Demo
{
    // Retries exactly once, immediately, no backoff - a policy this simple wouldn't be
    // worth shipping as a package default (most consumers want the real DefaultRetryPolicy's
    // exponential backoff), but it's a realistic ask for something like an unskippable
    // splash-screen asset where waiting for backoff delay is worse than failing fast.
    public sealed class SingleRetryPolicy : IRetryPolicy
    {
        public RetryDecision ShouldRetry(in RetryContext context) =>
            context.AttemptNumber < 2 ? RetryDecision.Retry(TimeSpan.Zero) : RetryDecision.Stop();
    }
}
