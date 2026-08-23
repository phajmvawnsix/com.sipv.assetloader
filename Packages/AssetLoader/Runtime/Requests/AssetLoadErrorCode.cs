namespace SiPV.AssetLoader
{
    /// <summary>
    /// Failure category on <see cref="AssetLoadException"/>, so callers branch on an enum instead
    /// of type-checking inner exceptions or parsing messages.
    /// </summary>
    public enum AssetLoadErrorCode
    {
        /// <summary>Nothing more specific applied. Inspect the inner exception.</summary>
        Unknown,

        /// <summary>
        /// The source could not deliver bytes: network error, a non-2xx and non-304 response, or
        /// retries exhausted. Check <see cref="AssetLoadException.HttpStatusCode"/> to tell a real
        /// HTTP response apart from a transport-level failure.
        /// </summary>
        FetchFailed,

        /// <summary>
        /// A content processor threw while transforming the bytes, for example a decrypt step given
        /// the wrong key.
        /// </summary>
        ProcessingFailed,

        /// <summary>
        /// No decoder matched the requested type, or the matching decoder threw. Most often means a
        /// missing <see cref="AssetLoaderConfigBuilder.RegisterDecoder{T}"/> call, or content whose
        /// bytes do not match its declared type.
        /// </summary>
        DecodeFailed,

        /// <summary>
        /// A timeout policy elapsed: either a single attempt exceeded its budget, or the overall
        /// deadline across retries did.
        /// </summary>
        TimedOut,

        /// <summary>Reserved for cancellation surfaced as an exception. The pipeline normally throws <see cref="System.OperationCanceledException"/> instead.</summary>
        Cancelled
    }
}
