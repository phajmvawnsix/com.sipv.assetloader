using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace SiPV.AssetLoader
{
    // Default IHttpClient, backed by UnityWebRequest.
    public sealed class UnityWebRequestHttpClient : IHttpClient
    {
        public async UniTask<HttpResponse> GetAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var webRequest = UnityWebRequest.Get(request.Url);

            if (request.Headers != null)
            {
                foreach (var header in request.Headers)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }
            }

            var operation = webRequest.SendWebRequest();
            
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    webRequest.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                await UniTask.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError
                || webRequest.result == UnityWebRequest.Result.DataProcessingError)
            {
                return HttpResponse.NetworkError(webRequest.error);
            }
            
            var body = webRequest.downloadHandler != null ? webRequest.downloadHandler.data : null;
            var headers = webRequest.GetResponseHeaders() ?? new Dictionary<string, string>();

            return new HttpResponse(webRequest.responseCode, body, headers);
        }
    }
}
