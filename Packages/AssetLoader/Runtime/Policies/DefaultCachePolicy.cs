using System;

namespace SiPV.AssetLoader
{
    /// <summary>Default <see cref="ICachePolicy"/>: standard HTTP-style freshness plus per-request overrides.</summary>
    /// <remarks>
    /// Freshness comes from <see cref="CacheEntryMetadata.IsFresh"/>. Honors the two overrides
    /// already defined on <see cref="AssetRequestFlags"/>: <c>ForceRefetch</c> skips the cache
    /// entirely (treated as a miss), <c>ForceRevalidate</c> always revalidates even if the entry
    /// is still fresh.
    /// </remarks>
    public sealed class DefaultCachePolicy : ICachePolicy
    {
        /// <inheritdoc />
        public CacheLookupResult Evaluate(in AssetRequest request, CacheEntryMetadata? existingMetadata, DateTimeOffset nowUtc)
        {
            if (request.Flags.HasFlag(AssetRequestFlags.ForceRefetch))
            {
                return CacheLookupResult.Miss;
            }

            if (!existingMetadata.HasValue)
            {
                return CacheLookupResult.Miss;
            }

            if (request.Flags.HasFlag(AssetRequestFlags.ForceRevalidate))
            {
                return CacheLookupResult.StaleRevalidate;
            }

            return existingMetadata.Value.IsFresh(nowUtc) ? CacheLookupResult.Fresh : CacheLookupResult.StaleRevalidate;
        }
    }
}
