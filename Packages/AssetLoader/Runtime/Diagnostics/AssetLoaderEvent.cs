using System;

namespace SiPV.AssetLoader
{
    /// <summary>Kind of event reported to <see cref="IAssetLoaderEventSink"/>.</summary>
    public enum AssetLoaderEventKind
    {
        /// <summary>The asset was already resident in the RAM cache.</summary>
        RamCacheHit,

        /// <summary>The asset was served from disk without a revalidation round trip.</summary>
        DiskCacheHit,

        /// <summary>The disk entry was revalidated against the source (conditional GET) and reused.</summary>
        DiskCacheRevalidated,

        /// <summary>Neither cache tier had the asset; a full fetch was required.</summary>
        CacheMiss,

        /// <summary>A concurrent request for the same key was coalesced onto an already in-flight fetch.</summary>
        DedupCoalesced,

        /// <summary>A failed attempt was retried per the active <see cref="IRetryPolicy"/>.</summary>
        RetryAttempted,

        /// <summary>The load ultimately failed.</summary>
        LoadFailed
    }

    /// <summary>Lightweight telemetry event, for example a demo HUD showing cache hit ratio, or production analytics.</summary>
    public readonly struct AssetLoaderEvent
    {
        /// <summary>What kind of event this is.</summary>
        public AssetLoaderEventKind Kind { get; }

        /// <summary>The cache key (or URL) the event concerns.</summary>
        public string Key { get; }

        /// <summary>
        /// How long the operation took, when meaningful. Null for events with no natural duration,
        /// such as a cache miss recorded before the fetch starts.
        /// </summary>
        public TimeSpan? Duration { get; }

        /// <summary>Creates a diagnostic event.</summary>
        public AssetLoaderEvent(AssetLoaderEventKind kind, string key, TimeSpan? duration = null)
        {
            Kind = kind;
            Key = key;
            Duration = duration;
        }
    }
}
