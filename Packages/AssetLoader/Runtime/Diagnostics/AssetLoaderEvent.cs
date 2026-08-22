using System;

namespace SiPV.AssetLoader
{
    // Kind of event reported to IAssetLoaderEventSink.
    public enum AssetLoaderEventKind
    {
        RamCacheHit,
        DiskCacheHit,
        DiskCacheRevalidated,
        CacheMiss,
        DedupCoalesced,
        RetryAttempted,
        LoadFailed
    }

    // Lightweight telemetry event, e.g. for a demo HUD showing cache hit ratio, or production analytics.
    public readonly struct AssetLoaderEvent
    {
        public AssetLoaderEventKind Kind { get; }

        public string Key { get; }

        public TimeSpan? Duration { get; }

        // Duration can be null - not all events have meaningful timing (e.g. cache miss recorded before fetch starts).
        public AssetLoaderEvent(AssetLoaderEventKind kind, string key, TimeSpan? duration = null)
        {
            Kind = kind;
            Key = key;
            Duration = duration;
        }
    }
}
