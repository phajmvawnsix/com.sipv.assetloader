using System;

namespace SiPV.AssetLoader
{
    // Default ITimeoutPolicy. Same fixed per-attempt duration for every request; overall deadline
    // defaults to null (no cap - retries are bounded only by IRetryPolicy's own attempt count).
    public sealed class DefaultTimeoutPolicy : ITimeoutPolicy
    {
        private readonly TimeSpan _perAttemptTimeout;
        private readonly TimeSpan? _overallDeadline;

        public DefaultTimeoutPolicy(TimeSpan? perAttemptTimeout = null, TimeSpan? overallDeadline = null)
        {
            _perAttemptTimeout = perAttemptTimeout ?? TimeSpan.FromSeconds(15);
            _overallDeadline = overallDeadline;
        }

        public TimeSpan GetTimeout(in AssetRequest request) => _perAttemptTimeout;

        public TimeSpan? GetOverallDeadline(in AssetRequest request) => _overallDeadline;
    }
}
