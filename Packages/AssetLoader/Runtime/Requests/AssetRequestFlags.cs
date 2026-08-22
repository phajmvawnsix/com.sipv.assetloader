using System;

namespace SiPV.AssetLoader
{
    /// <summary>Per-request bypass/refresh flags. Combine with bitwise OR.</summary>
    [Flags]
    public enum AssetRequestFlags
    {
        None = 0,

        // still populates the tier on success, just skips reading from it
        BypassRamCache = 1 << 0,
        BypassDiskCache = 1 << 1,

        // forces a conditional GET even if still within max-age
        ForceRevalidate = 1 << 2,

        // re-downloads unconditionally, overwrites any cached entry
        ForceRefetch = 1 << 3
    }
}
