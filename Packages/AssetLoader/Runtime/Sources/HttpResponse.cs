using System.Collections.Generic;

namespace SiPV.AssetLoader
{
    public readonly struct HttpResponse
    {
        public long StatusCode { get; }

        public byte[] Body { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public string NetworkErrorMessage { get; }

        public bool IsNetworkError => NetworkErrorMessage != null;

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

        public static HttpResponse NetworkError(string message) => new HttpResponse(message);
    }
}
