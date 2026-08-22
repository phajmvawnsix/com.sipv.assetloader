using System;

namespace SiPV.AssetLoader
{
    /// <summary>Decides whether to serve from cache, revalidate, or treat as a miss.</summary>
    public interface ICachePolicy
    {
        CacheLookupResult Evaluate(in AssetRequest request, CacheEntryMetadata? existingMetadata, DateTimeOffset nowUtc);
    }
}
