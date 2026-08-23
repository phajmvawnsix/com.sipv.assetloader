using System.Collections.Generic;

namespace SiPV.AssetLoader
{
    /// <summary>A transport-agnostic HTTP response, returned by <see cref="IHttpClient.GetAsync"/>.</summary>
    /// <remarks>
    /// Distinguishes an HTTP-level failure (a status code, still a valid response) from a
    /// network-level one (<see cref="IsNetworkError"/>, no status code available at all) so
    /// <see cref="HttpAssetSource"/> can tell them apart.
    /// </remarks>
    public readonly struct HttpResponse
    {
        /// <summary>The HTTP status code. Meaningless when <see cref="IsNetworkError"/> is true.</summary>
        public long StatusCode { get; }

        /// <summary>The response body bytes.</summary>
        public byte[] Body { get; }

        /// <summary>The response headers.</summary>
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>The transport-level error message, set only for a network error.</summary>
        public string NetworkErrorMessage { get; }

        /// <summary>True when the request failed before reaching a valid HTTP response (connection or data processing error).</summary>
        public bool IsNetworkError => NetworkErrorMessage != null;

        /// <summary>Creates a successful HTTP response.</summary>
        public HttpResponse(long statusCode, byte[] body, IReadOnlyDictionary<string, string> headers)
        {
            StatusCode = statusCode;
            Body = body;
            Headers = headers;
            NetworkErrorMessage = null;
        }

        private HttpResponse(string networkErrorMessage)
        {
            StatusCode = 0;
            Body = null;
            Headers = null;
            NetworkErrorMessage = networkErrorMessage;
        }

        /// <summary>Creates a response representing a network-level failure (no HTTP status available).</summary>
        public static HttpResponse NetworkError(string message) => new HttpResponse(message);
    }
}
