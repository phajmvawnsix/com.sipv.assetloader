using System;

namespace SiPV.AssetLoader
{
    /// <summary>Per-request cache bypass and refresh switches. Combine with bitwise OR.</summary>
    [Flags]
    public enum AssetRequestFlags
    {
        /// <summary>Normal behavior: read from both cache tiers, respect the cache policy.</summary>
        None = 0,

        /// <summary>
        /// Skips reading from the RAM cache, forcing at least a disk read. The tier is still
        /// populated on success, so the next normal load hits it.
        /// </summary>
        BypassRamCache = 1 << 0,

        /// <summary>
        /// Skips reading from the disk cache, forcing a source fetch. Bytes are still written to
        /// disk on success. Note that with no cached metadata to compare against, a source that
        /// answers 304 Not Modified in this state has nothing to revalidate and the load fails.
        /// </summary>
        BypassDiskCache = 1 << 1,

        /// <summary>
        /// Forces a conditional request even when the cached entry is still within its max-age.
        /// The server may answer 304, in which case the cached bytes are reused and only the
        /// freshness timestamps update.
        /// </summary>
        ForceRevalidate = 1 << 2,

        /// <summary>
        /// Skips revalidation entirely and redownloads, overwriting whatever was cached. Use for a
        /// forced content refresh; prefer <see cref="ForceRevalidate"/> when unchanged content
        /// should still avoid the download.
        /// </summary>
        ForceRefetch = 1 << 3
    }
}
