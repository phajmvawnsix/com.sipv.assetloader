using System;
using System.Collections.Generic;

namespace SiPV.AssetLoader
{
    // Input to IAssetSource.FetchAsync - request URL plus what's known from a prior cached fetch, so the source can do a conditional GET.
    public readonly struct AssetSourceRequestContext
    {
        public string Url { get; }

        // sent as If-None-Match
        public string ETagIfKnown { get; }

        // sent as If-Modified-Since; fallback for sources without ETag support
        public DateTimeOffset? LastModifiedIfKnown { get; }

        public IReadOnlyDictionary<string, string> CustomHeaders { get; }

        public ITimeoutPolicy TimeoutPolicy { get; }

        // from AssetRequest.Priority, for sources that throttle/queue concurrent fetches; default HTTP source may ignore it
        public AssetRequestPriority Priority { get; }

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
