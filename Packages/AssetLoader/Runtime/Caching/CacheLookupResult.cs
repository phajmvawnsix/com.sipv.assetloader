namespace SiPV.AssetLoader
{
    // Cache policy decision for a lookup.
    public enum CacheLookupResult
    {
        // within max-age, serve directly
        Fresh,

        // stale, needs a conditional GET to check for a 304
        StaleRevalidate,

        // nothing usable, full fetch needed
        Miss
    }
}
