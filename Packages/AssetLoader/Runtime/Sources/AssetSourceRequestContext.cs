using System;
using System.Collections.Generic;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Input to <see cref="IAssetSource.FetchAsync"/>: the request URL plus what is known from a
    /// prior cached fetch, so the source can do a conditional GET.
    /// </summary>
    public readonly struct AssetSourceRequestContext
    {
        /// <summary>The URL to fetch.</summary>
        public string Url { get; }

        /// <summary>Sent as <c>If-None-Match</c>, if a prior fetch recorded one.</summary>
        public string ETagIfKnown { get; }

        /// <summary>Sent as <c>If-Modified-Since</c>, fallback for sources without ETag support.</summary>
        public DateTimeOffset? LastModifiedIfKnown { get; }

        /// <summary>Additional headers to send with the request.</summary>
        public IReadOnlyDictionary<string, string> CustomHeaders { get; }

        /// <summary>The timeout policy in effect for this request.</summary>
        public ITimeoutPolicy TimeoutPolicy { get; }

        /// <summary>
        /// From <see cref="AssetRequest.Priority"/>, for sources that throttle or queue concurrent
        /// fetches. The default HTTP source ignores it.
        /// </summary>
        public AssetRequestPriority Priority { get; }

        /// <summary>Creates a source request context.</summary>
        public AssetSourceRequestContext(
            string url,
            string eTagIfKnown,
            DateTimeOffset? lastModifiedIfKnown,
            IReadOnlyDictionary<string, string> customHeaders,
            ITimeoutPolicy timeoutPolicy,
            AssetRequestPriority priority)
        {
            Url = url;
            ETagIfKnown = eTagIfKnown;
            LastModifiedIfKnown = lastModifiedIfKnown;
            CustomHeaders = customHeaders;
            TimeoutPolicy = timeoutPolicy;
            Priority = priority;
        }
    }
}
