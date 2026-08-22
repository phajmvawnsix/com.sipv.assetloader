using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    public sealed class HttpAssetSource : IAssetSource
    {
        private readonly IHttpClient _httpClient;

        public HttpAssetSource(IHttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async UniTask<AssetSourceResult> FetchAsync(AssetSourceRequestContext context, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(context.Url, BuildRequestHeaders(context));
            var response = await _httpClient.GetAsync(request, cancellationToken);

            if (response.IsNetworkError)
            {
                return Failed(context.Url, $"Network error: {response.NetworkErrorMessage}");
            }

            if (response.StatusCode == 304)
            {
                return BuildNotModifiedResult(response);
            }

            if (response.StatusCode >= 200 && response.StatusCode < 300)
            {
                return BuildOkResult(response);
            }

            return Failed(context.Url, $"HTTP {response.StatusCode}");
        }

        private static IReadOnlyDictionary<string, string> BuildRequestHeaders(AssetSourceRequestContext context)
        {
            var headers = new Dictionary<string, string>();

            if (context.CustomHeaders != null)
            {
                foreach (var header in context.CustomHeaders)
                {
                    headers[header.Key] = header.Value;
                }
            }

            if (!string.IsNullOrEmpty(context.ETagIfKnown))
            {
                headers["If-None-Match"] = context.ETagIfKnown;
            }
            else if (context.LastModifiedIfKnown.HasValue)
            {
                headers["If-Modified-Since"] = context.LastModifiedIfKnown.Value.UtcDateTime.ToString("R");
            }

            return headers;
        }
        
        private static AssetSourceResult BuildNotModifiedResult(HttpResponse response)
        {
            var cacheControl = GetHeader(response.Headers, "Cache-Control");
            var noStore = HasDirective(cacheControl, "no-store");
            var eTag = noStore ? null : GetHeader(response.Headers, "ETag");
            
            var maxAge = (noStore || string.IsNullOrEmpty(cacheControl)) ? (TimeSpan?)null : ParseMaxAge(cacheControl);

            return new AssetSourceResult(AssetSourceStatus.NotModified304, null, eTag, maxAge, DateTimeOffset.UtcNow, null);
        }

        private static AssetSourceResult BuildOkResult(HttpResponse response)
        {
            var cacheControl = GetHeader(response.Headers, "Cache-Control");
            var noStore = HasDirective(cacheControl, "no-store");

            var eTag = noStore ? null : GetHeader(response.Headers, "ETag");
            var maxAge = noStore ? (TimeSpan?)null : ParseMaxAge(cacheControl);
            var contentType = GetHeader(response.Headers, "Content-Type");

            return new AssetSourceResult(AssetSourceStatus.Ok200, response.Body, eTag, maxAge, DateTimeOffset.UtcNow, contentType);
        }

        private static AssetSourceResult Failed(string url, string message) => new AssetSourceResult(
            AssetSourceStatus.Failed, null, null, null, DateTimeOffset.UtcNow, null,
            new AssetLoadException(AssetLoadErrorCode.FetchFailed, url, message));
        
        private static TimeSpan? ParseMaxAge(string cacheControl)
        {
            if (string.IsNullOrEmpty(cacheControl) || HasDirective(cacheControl, "no-cache"))
            {
                return TimeSpan.Zero;
            }

            foreach (var raw in cacheControl.Split(','))
            {
                var directive = raw.Trim();
                if (!directive.StartsWith("max-age", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var eq = directive.IndexOf('=');
                if (eq >= 0 && int.TryParse(directive.Substring(eq + 1).Trim(), out var seconds) && seconds >= 0)
                {
                    return TimeSpan.FromSeconds(seconds);
                }

                return TimeSpan.Zero;
            }

            return TimeSpan.Zero;
        }

        private static bool HasDirective(string cacheControl, string directiveName)
        {
            if (string.IsNullOrEmpty(cacheControl))
            {
                return false;
            }

            foreach (var raw in cacheControl.Split(','))
            {
                if (raw.Trim().Equals(directiveName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetHeader(IReadOnlyDictionary<string, string> headers, string name)
        {
            if (headers == null)
            {
                return null;
            }

            foreach (var header in headers)
            {
                if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return header.Value;
                }
            }

            return null;
        }
    }
}
