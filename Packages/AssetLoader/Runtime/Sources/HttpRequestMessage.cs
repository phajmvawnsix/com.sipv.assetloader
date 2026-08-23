using System.Collections.Generic;

namespace SiPV.AssetLoader
{
    /// <summary>A transport-agnostic HTTP GET request, passed to <see cref="IHttpClient.GetAsync"/>.</summary>
    public readonly struct HttpRequestMessage
    {
        /// <summary>The URL to fetch.</summary>
        public string Url { get; }

        /// <summary>Headers to send with the request, including any conditional-GET headers.</summary>
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>Creates an HTTP request message.</summary>
        public HttpRequestMessage(string url, IReadOnlyDictionary<string, string> headers)
        {
            Url = url;
            Headers = headers;
        }
    }
}
