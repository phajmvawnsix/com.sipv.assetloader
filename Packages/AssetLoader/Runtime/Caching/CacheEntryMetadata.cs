using System;

namespace SiPV.AssetLoader
{
    /// <summary>Metadata persisted alongside a cached asset (ETag, max-age, timestamps, size).</summary>
    public readonly struct CacheEntryMetadata
    {
        public string ETag { get; }

        public TimeSpan? MaxAge { get; }

        public DateTimeOffset FetchedAtUtc { get; }

        public DateTimeOffset LastAccessUtc { get; }

        public long SizeBytes { get; }

        public string ContentType { get; }

        public CacheEntryMetadata(
            string eTag,
            TimeSpan? maxAge,
            DateTimeOffset fetchedAtUtc,
            DateTimeOffset lastAccessUtc,
            long sizeBytes,
            string contentType)
        {
            ETag = eTag;
            MaxAge = maxAge;
            FetchedAtUtc = fetchedAtUtc;
            LastAccessUtc = lastAccessUtc;
            SizeBytes = sizeBytes;
            ContentType = contentType;
        }

        // null MaxAge means never fresh, i.e. always revalidate.
        // TODO: check this against what the CDN actually sends - if it omits Cache-Control we'd be
        // hammering it with conditional GETs on every single load.
        public bool IsFresh(DateTimeOffset nowUtc) =>
            MaxAge.HasValue && (nowUtc - FetchedAtUtc) < MaxAge.Value;

        // bump LastAccessUtc on every read, stores rely on this for LRU ordering
        public CacheEntryMetadata WithLastAccess(DateTimeOffset nowUtc) =>
            new CacheEntryMetadata(ETag, MaxAge, FetchedAtUtc, nowUtc, SizeBytes, ContentType);
    }
}
