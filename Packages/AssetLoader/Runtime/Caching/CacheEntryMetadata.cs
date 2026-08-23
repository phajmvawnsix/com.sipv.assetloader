using System;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Freshness and bookkeeping data persisted alongside a cached asset: validator, lifetime,
    /// timestamps, size.
    /// </summary>
    /// <remarks>
    /// Kept separate from the bytes themselves so the two stores can be swapped independently. Both
    /// cache tiers use it: the disk tier persists it, and the RAM tier carries it for budget
    /// accounting.
    /// </remarks>
    public readonly struct CacheEntryMetadata
    {
        /// <summary>
        /// The server's content validator, sent back as If-None-Match to revalidate. Null when the
        /// server did not provide one, in which case revalidation cannot produce a 304.
        /// </summary>
        public string ETag { get; }

        /// <summary>
        /// How long after <see cref="FetchedAtUtc"/> the entry may be served without asking the
        /// server. Null means the server said nothing about lifetime, which
        /// <see cref="IsFresh"/> treats as never fresh.
        /// </summary>
        public TimeSpan? MaxAge { get; }

        /// <summary>When the bytes were downloaded. The baseline for freshness.</summary>
        public DateTimeOffset FetchedAtUtc { get; }

        /// <summary>When the entry was last served. Drives least-recently-used eviction ordering.</summary>
        public DateTimeOffset LastAccessUtc { get; }

        /// <summary>Size of the cached bytes, used for budget accounting.</summary>
        public long SizeBytes { get; }

        /// <summary>
        /// Content type as reported by the source. Used to resolve a decoder when the URL has no
        /// useful file extension.
        /// </summary>
        public string ContentType { get; }

        /// <summary>Creates a metadata record.</summary>
        /// <param name="eTag">Server validator, or null.</param>
        /// <param name="maxAge">Freshness lifetime, or null if unknown.</param>
        /// <param name="fetchedAtUtc">Download time.</param>
        /// <param name="lastAccessUtc">Last served time.</param>
        /// <param name="sizeBytes">Cached byte count.</param>
        /// <param name="contentType">Reported content type, or null.</param>
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

        /// <summary>Whether the entry can be served without contacting the server.</summary>
        /// <param name="nowUtc">Current time.</param>
        /// <returns>True while still inside <see cref="MaxAge"/>.</returns>
        /// <remarks>
        /// A null <see cref="MaxAge"/> is never fresh, so content served without cache headers
        /// revalidates on every load rather than being trusted indefinitely.
        /// </remarks>
        // TODO: worth checking against what your CDN actually sends. A CDN that omits Cache-Control
        // entirely will get a conditional GET on every single load under this rule.
        public bool IsFresh(DateTimeOffset nowUtc) =>
            MaxAge.HasValue && (nowUtc - FetchedAtUtc) < MaxAge.Value;

        /// <summary>Returns a copy with a refreshed last-access time.</summary>
        /// <param name="nowUtc">New last-access time.</param>
        /// <returns>An updated copy. The original is unchanged, this being a struct.</returns>
        /// <remarks>Call on every read: eviction ordering depends on it staying current.</remarks>
        public CacheEntryMetadata WithLastAccess(DateTimeOffset nowUtc) =>
            new CacheEntryMetadata(ETag, MaxAge, FetchedAtUtc, nowUtc, SizeBytes, ContentType);
    }
}
