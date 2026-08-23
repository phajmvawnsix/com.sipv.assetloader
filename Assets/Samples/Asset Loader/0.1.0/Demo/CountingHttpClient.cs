using System.Threading;
using Cysharp.Threading.Tasks;
using SiPV.AssetLoader;

namespace SiPV.AssetLoader.Samples.Demo
{
    // Wraps the real IHttpClient just to count how many actual HTTP round-trips happen -
    // the dedup demo button proves "5 concurrent LoadAsync calls share 1 fetch" by comparing
    // this counter's delta against the number of buttons clicked, not by trusting the UI alone.
    public sealed class CountingHttpClient : IHttpClient
    {
        private readonly IHttpClient _inner;
        private int _requestCount;

        public int RequestCount => _requestCount;

        public CountingHttpClient(IHttpClient inner)
        {
            _inner = inner;
        }

        public UniTask<HttpResponse> GetAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            return _inner.GetAsync(request, cancellationToken);
        }
    }
}
