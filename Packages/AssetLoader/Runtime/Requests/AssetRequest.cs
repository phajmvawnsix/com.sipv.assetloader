using System.Collections.Generic;

namespace SiPV.AssetLoader
{
    /// <summary>Describes a single load request: what to fetch, how to key it in caches, and any per-request overrides.</summary>
    public readonly struct AssetRequest
    {
        public string Url { get; }

        // overrides Url as the cache key when set
        public string Key { get; }

        // folded into the disk cache key hash so variants of the same URL don't collide
        public string Variant { get; }

        public AssetRequestPriority Priority { get; }

        public AssetRequestFlags Flags { get; }

        public ITimeoutPolicy TimeoutPolicyOverride { get; }

        public IRetryPolicy RetryPolicyOverride { get; }

        public ICachePolicy CachePolicyOverride { get; }

        public IReadOnlyDictionary<string, string> CustomHeaders { get; }

        public object UserData { get; }

        public AssetRequest(
            string url,
            string key = null,
            string variant = null,
            AssetRequestPriority priority = AssetRequestPriority.Normal,
            AssetRequestFlags flags = AssetRequestFlags.None,
            ITimeoutPolicy timeoutPolicyOverride = null,
            IRetryPolicy retryPolicyOverride = null,
            ICachePolicy cachePolicyOverride = null,
            IReadOnlyDictionary<string, string> customHeaders = null,
            object userData = null)
        {
            Url = url;
            Key = key;
            Variant = variant;
            Priority = priority;
            Flags = flags;
            TimeoutPolicyOverride = timeoutPolicyOverride;
            RetryPolicyOverride = retryPolicyOverride;
            CachePolicyOverride = cachePolicyOverride;
            CustomHeaders = customHeaders;
            UserData = userData;
        }

        // Key (or Url if unset) + ":" + Variant when present. RAM cache uses this verbatim, disk cache hashes it.
        // TODO: allocates a string per call on the RAM-hit path. Cache it on first use if it shows up in a profile.
        public string ResolveCacheSeed()
        {
            var seed = string.IsNullOrEmpty(Key) ? Url : Key;
            return string.IsNullOrEmpty(Variant) ? seed : seed + ":" + Variant;
        }
    }
}
