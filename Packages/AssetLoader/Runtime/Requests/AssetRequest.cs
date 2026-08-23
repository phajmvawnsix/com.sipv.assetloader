using System.Collections.Generic;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Describes a single load: what to fetch, how to key it in the caches, and any per-request
    /// overrides.
    /// </summary>
    /// <remarks>
    /// A struct, so passing one around costs nothing and there is no null case to guard. Every
    /// field past <c>url</c> is optional, so the common call is just
    /// <c>new AssetRequest("https://...")</c>.
    /// </remarks>
    public readonly struct AssetRequest
    {
        /// <summary>Where the bytes come from. Also the default cache key when <see cref="Key"/> is unset.</summary>
        public string Url { get; }

        /// <summary>
        /// Replaces <see cref="Url"/> as the cache key. Set this when the URL is unstable but the
        /// content is not, for example a signed URL whose token changes every session: keying on
        /// the URL would miss the cache every time.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Folded into the cache key so variants of the same source do not collide, for example a
        /// texture requested at two quality levels through the same URL.
        /// </summary>
        public string Variant { get; }

        /// <summary>Hint passed through to <see cref="IAssetSource"/> implementations that support prioritisation.</summary>
        public AssetRequestPriority Priority { get; }

        /// <summary>Per-request cache bypass and forced-refresh switches.</summary>
        public AssetRequestFlags Flags { get; }

        /// <summary>Overrides the loader's global timeout policy for this request only. Null uses the global one.</summary>
        public ITimeoutPolicy TimeoutPolicyOverride { get; }

        /// <summary>Overrides the loader's global retry policy for this request only. Null uses the global one.</summary>
        public IRetryPolicy RetryPolicyOverride { get; }

        /// <summary>Overrides the loader's global cache-freshness policy for this request only. Null uses the global one.</summary>
        public ICachePolicy CachePolicyOverride { get; }

        /// <summary>
        /// Extra HTTP headers merged into the source request, for example an auth token. Conditional
        /// headers the pipeline manages itself (If-None-Match, If-Modified-Since) take precedence.
        /// </summary>
        public IReadOnlyDictionary<string, string> CustomHeaders { get; }

        /// <summary>
        /// Opaque payload forwarded untouched to content processors and decoders. Use it to pass a
        /// decryption key or decode parameters to your own extensions; the pipeline never inspects it.
        /// </summary>
        public object UserData { get; }

        /// <summary>Creates a request. Only <paramref name="url"/> is required.</summary>
        /// <param name="url">Where to fetch from.</param>
        /// <param name="key">Cache key override. See <see cref="Key"/>.</param>
        /// <param name="variant">Cache key discriminator. See <see cref="Variant"/>.</param>
        /// <param name="priority">Source-level priority hint.</param>
        /// <param name="flags">Cache bypass and forced-refresh switches.</param>
        /// <param name="timeoutPolicyOverride">Per-request timeout policy, or null for the global one.</param>
        /// <param name="retryPolicyOverride">Per-request retry policy, or null for the global one.</param>
        /// <param name="cachePolicyOverride">Per-request freshness policy, or null for the global one.</param>
        /// <param name="customHeaders">Extra headers for the source request.</param>
        /// <param name="userData">Opaque payload for your own processors and decoders.</param>
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

        /// <summary>
        /// Builds the string both cache tiers key off: <see cref="Key"/> (or <see cref="Url"/> when
        /// unset), plus <see cref="Variant"/> when present. The RAM cache uses it verbatim, the disk
        /// cache hashes it.
        /// </summary>
        /// <returns>The resolved cache seed.</returns>
        /// <remarks>
        /// Allocation-free in the common case: with no variant it returns the existing string
        /// reference rather than building a new one. A variant costs one concatenation per call.
        /// </remarks>
        public string ResolveCacheSeed()
        {
            var seed = string.IsNullOrEmpty(Key) ? Url : Key;
            return string.IsNullOrEmpty(Variant) ? seed : seed + ":" + Variant;
        }
    }
}
