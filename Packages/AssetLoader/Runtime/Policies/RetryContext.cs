using System;

namespace SiPV.AssetLoader
{
    /// <summary>Describes a failure that just occurred, passed to <see cref="IRetryPolicy.ShouldRetry"/>.</summary>
    /// <remarks>Built by the pipeline itself, not meant to be constructed by policy implementations.</remarks>
    public readonly struct RetryContext
    {
        /// <summary>The request that failed.</summary>
        public AssetRequest Request { get; }

        /// <summary>1-based; the attempt that just failed.</summary>
        public int AttemptNumber { get; }

        /// <summary>The exception the failed attempt threw.</summary>
        public Exception LastException { get; }

        /// <summary>Which pipeline stage the failure happened in.</summary>
        public RetryStage Stage { get; }

        /// <summary>Creates a retry context describing a failed attempt.</summary>
        public RetryContext(AssetRequest request, int attemptNumber, Exception lastException, RetryStage stage)
        {
            Request = request;
            AttemptNumber = attemptNumber;
            LastException = lastException;
            Stage = stage;
        }
    }
}
