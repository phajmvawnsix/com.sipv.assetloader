using System;

namespace SiPV.AssetLoader
{
    /// <summary>Result of an IAssetSource fetch attempt.</summary>
    public readonly struct AssetSourceResult
    {
        /// <summary>Whether the fetch succeeded, was a 304-not-modified, or failed.</summary>
        public AssetSourceStatus Status { get; }

        /// <summary>The downloaded bytes. Null when <see cref="Status"/> is <c>NotModified304</c> or <c>Failed</c>.</summary>
        public byte[] RawBytes { get; }

        /// <summary>The response's ETag, used to revalidate the entry on a later request.</summary>
        public string ETag { get; }

        /// <summary>Parsed <c>Cache-Control: max-age</c> from the response, if present.</summary>
        public TimeSpan? MaxAge { get; }

        /// <summary>When this result was produced, used to compute freshness later.</summary>
        public DateTimeOffset FetchedAtUtc { get; }

        /// <summary>The response's content type, used to resolve a decoder.</summary>
        public string ContentType { get; }

        /// <summary>The failure, set when <see cref="Status"/> is <c>Failed</c>.</summary>
        public AssetLoadException Error { get; }

        /// <summary>Creates a source fetch result.</summary>
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
