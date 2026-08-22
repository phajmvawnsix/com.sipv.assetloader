using System;

namespace SiPV.AssetLoader
{
    /// <summary>Result of an IAssetSource fetch attempt.</summary>
    public readonly struct AssetSourceResult
    {
        public AssetSourceStatus Status { get; }

        // null when Status is NotModified304 or Failed
        public byte[] RawBytes { get; }

        public string ETag { get; }

        // parsed Cache-Control: max-age, if present
        public TimeSpan? MaxAge { get; }

        public DateTimeOffset FetchedAtUtc { get; }

        public string ContentType { get; }

        // set when Status is Failed
        public AssetLoadException Error { get; }

        public AssetSourceResult(
            AssetSourceStatus status,
            byte[] rawBytes,
            string eTag,
            TimeSpan? maxAge,
            DateTimeOffset fetchedAtUtc,
            string contentType,
            AssetLoadException error = null)
        {
            Status = status;
            RawBytes = rawBytes;
            ETag = eTag;
            MaxAge = maxAge;
            FetchedAtUtc = fetchedAtUtc;
            ContentType = contentType;
            Error = error;
        }
    }
}
