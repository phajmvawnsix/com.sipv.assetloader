using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    public interface IHttpClient
    {
        UniTask<HttpResponse> GetAsync(HttpRequestMessage request, CancellationToken cancellationToken);
    }
}
