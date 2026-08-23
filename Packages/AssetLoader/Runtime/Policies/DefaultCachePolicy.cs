using System;

namespace SiPV.AssetLoader
{
    // Default ICachePolicy. Standard HTTP-style freshness (CacheEntryMetadata.IsFresh) plus the two
    // per-request overrides AssetRequestFlags already defines: ForceRefetch & ForceRevalidate
    public sealed class DefaultCachePolicy : ICachePolicy
    {
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
