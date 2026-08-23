using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader.Tests
{
    // No real network/UnityWebRequest - HttpAssetSource's HTTP-semantics logic (ETag/Cache-Control/
    // status handling) is what's under test here, not the transport.
    internal sealed class FakeHttpClient : IHttpClient
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public Func<HttpRequestMessage, HttpResponse> ResponseFactory =
            _ => throw new InvalidOperationException("FakeHttpClient.ResponseFactory not configured for this test.");

        public UniTask<HttpResponse> GetAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return UniTask.FromResult(ResponseFactory(request));
        }
    }
}
