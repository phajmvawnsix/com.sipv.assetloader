using System;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Single exception type for all load failures. Branch on ErrorCode rather than parsing the
    /// message or type-checking InnerException, which is only set when the underlying layer
    /// (UnityWebRequest, a decoder, a content processor) actually threw something worth keeping.
    /// </summary>
    public sealed class AssetLoadException : Exception
    {
        public AssetLoadErrorCode ErrorCode { get; }

        // resolved cache seed, not the raw URL - safe to log, already includes the variant
        public string RequestKey { get; }

        public AssetLoadException(AssetLoadErrorCode errorCode, string requestKey, string message, Exception innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            RequestKey = requestKey;
        }
    }
}
