namespace SiPV.AssetLoader
{
    /// <summary>What <see cref="ICachePolicy.Evaluate"/> decided about a cached entry.</summary>
    public enum CacheLookupResult
    {
        /// <summary>Still within its lifetime. Serve the cached bytes without contacting the source.</summary>
        Fresh,

        /// <summary>
        /// Cached but past its lifetime. The pipeline sends a conditional request; a 304 reuses the
        /// cached bytes and only refreshes timestamps, anything else replaces them.
        /// </summary>
        StaleRevalidate,

        /// <summary>Nothing usable cached. Fetch in full.</summary>
        Miss
    }
}
