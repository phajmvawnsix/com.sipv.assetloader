using System;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// The single exception type for load failures. Branch on <see cref="ErrorCode"/> rather than
    /// parsing the message or type-checking <see cref="Exception.InnerException"/>.
    /// </summary>
    /// <remarks>
    /// One exception type with a code beats a hierarchy of six: callers want "was this the network
    /// or the content" and would otherwise write six catch blocks. <see cref="Exception.InnerException"/>
    /// is set only when the underlying layer (UnityWebRequest, a decoder, a content processor)
    /// actually threw something worth keeping.
    /// </remarks>
    public sealed class AssetLoadException : Exception
    {
        /// <summary>Which stage failed and how. See <see cref="AssetLoadErrorCode"/>.</summary>
        public AssetLoadErrorCode ErrorCode { get; }

        /// <summary>
        /// The resolved cache seed rather than the raw URL, so it already accounts for any key or
        /// variant override. Safe to log.
        /// </summary>
        public string RequestKey { get; }

        /// <summary>
        /// The HTTP status when the failure came from a real response, otherwise null. A null value
        /// alongside <see cref="AssetLoadErrorCode.FetchFailed"/> means the request never got a
        /// response at all (DNS, timeout, connection refused), which is generally worth retrying
        /// where a 404 is not.
        /// </summary>
        public int? HttpStatusCode { get; }

        /// <summary>Creates a load exception.</summary>
        /// <param name="errorCode">Failure category.</param>
        /// <param name="requestKey">Resolved cache seed of the failed request.</param>
        /// <param name="message">Human-readable detail.</param>
        /// <param name="innerException">Underlying exception, when one exists.</param>
        /// <param name="httpStatusCode">HTTP status, when the failure was a real HTTP response.</param>
        public AssetLoadException(
            AssetLoadErrorCode errorCode, string requestKey, string message,
            Exception innerException = null, int? httpStatusCode = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            RequestKey = requestKey;
            HttpStatusCode = httpStatusCode;
        }
    }
}
