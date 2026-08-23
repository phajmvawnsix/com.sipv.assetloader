using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>Transport abstraction underneath <see cref="HttpAssetSource"/>.</summary>
    /// <remarks>
    /// Exists so <see cref="HttpAssetSource"/>'s conditional-GET and cache-header logic can be
    /// tested and reused without depending on <c>UnityWebRequest</c> directly. Implementations
    /// backed by <c>UnityWebRequest</c> must be called on the main thread.
    /// </remarks>
    public interface IHttpClient
    {
        /// <summary>Sends a GET request and returns the response, or a network-error response if the transport failed.</summary>
        /// <param name="request">The URL and headers to send.</param>
        /// <param name="cancellationToken">Cancels the in-flight request.</param>
        UniTask<HttpResponse> GetAsync(HttpRequestMessage request, CancellationToken cancellationToken);
    }
}
