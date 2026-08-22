using System.Collections.Generic;

namespace SiPV.AssetLoader
{
    public readonly struct HttpRequestMessage
    {
        public string Url { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public HttpRequestMessage(string url, IReadOnlyDictionary<string, string> headers)
        {
            Url = url;
            Headers = headers;
        }
    }
}
