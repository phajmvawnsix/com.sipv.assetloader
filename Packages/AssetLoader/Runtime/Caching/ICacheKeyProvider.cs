namespace SiPV.AssetLoader
{
    // Derives RAM/disk cache keys from an AssetRequest. Override to customize keying (e.g. cache-busting on app version).
    public interface ICacheKeyProvider
    {
        // no filesystem-safety requirement, just needs to be a good dictionary key
        string GetRamKey(in AssetRequest request);

        // must be safe as a filename/path segment on every target platform
        string GetDiskKey(in AssetRequest request);
    }
}
