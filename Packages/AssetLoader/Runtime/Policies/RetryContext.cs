using System;

namespace SiPV.AssetLoader
{
    // Describes the failure that just occurred, passed to IRetryPolicy.ShouldRetry.
    public readonly struct RetryContext
    {
        public AssetRequest Request { get; }

        // 1-based; this is the attempt that just failed.
        public int AttemptNumber { get; }

        public Exception LastException { get; }

        public RetryStage Stage { get; }

        // Built by the pipeline itself, not meant to be constructed by policy implementations.
        public RetryContext(AssetRequest request, int attemptNumber, Exception lastException, RetryStage stage)
        {
            Request = request;
            AttemptNumber = attemptNumber;
            LastException = lastException;
            Stage = stage;
        }
    }
}
