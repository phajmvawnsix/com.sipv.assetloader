using System;

namespace SiPV.AssetLoader
{
    /// <summary>Default <see cref="ITimeoutPolicy"/>: a fixed per-attempt timeout, no overall deadline.</summary>
    /// <remarks>
    /// Same duration for every request regardless of <see cref="AssetRequest"/> content. The
    /// overall deadline defaults to null (no cap), so retries are bounded only by
    /// <see cref="IRetryPolicy"/>'s own attempt count, not by wall-clock time.
    /// </remarks>
    public sealed class DefaultTimeoutPolicy : ITimeoutPolicy
    {
        private readonly TimeSpan _perAttemptTimeout;
        private readonly TimeSpan? _overallDeadline;

        /// <summary>Creates a timeout policy with a fixed per-attempt timeout and optional overall deadline.</summary>
        /// <param name="perAttemptTimeout">Timeout applied to each individual attempt. Defaults to 15 seconds.</param>
        /// <param name="overallDeadline">Total wall-clock budget across all attempts. Defaults to null (unbounded).</param>
        public DefaultTimeoutPolicy(TimeSpan? perAttemptTimeout = null, TimeSpan? overallDeadline = null)
        {
            _perAttemptTimeout = perAttemptTimeout ?? TimeSpan.FromSeconds(15);
            _overallDeadline = overallDeadline;
        }

        /// <inheritdoc />
        public TimeSpan GetTimeout(in AssetRequest request) => _perAttemptTimeout;

        /// <inheritdoc />
        public TimeSpan? GetOverallDeadline(in AssetRequest request) => _overallDeadline;
    }
}
