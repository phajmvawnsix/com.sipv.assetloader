using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Typed sugar over IAssetLoader.LoadAsync<T> - just one-line forwards, nothing to update when new asset types land.
    public static class AssetLoaderExtensions
    {
        public static UniTask<AssetHandle<UnityEngine.Texture2D>> LoadTextureAsync(this IAssetLoader loader, string url, CancellationToken cancellationToken = default) =>
            loader.LoadAsync<UnityEngine.Texture2D>(new AssetRequest(url), cancellationToken);

        public static UniTask<AssetHandle<UnityEngine.AudioClip>> LoadAudioClipAsync(this IAssetLoader loader, string url, CancellationToken cancellationToken = default) =>
            loader.LoadAsync<UnityEngine.AudioClip>(new AssetRequest(url), cancellationToken);

        public static UniTask<AssetHandle<string>> LoadTextAsync(this IAssetLoader loader, string url, CancellationToken cancellationToken = default) =>
            loader.LoadAsync<string>(new AssetRequest(url), cancellationToken);

        // For formats without a dedicated decoder yet.
        public static UniTask<AssetHandle<byte[]>> LoadBytesAsync(this IAssetLoader loader, string url, CancellationToken cancellationToken = default) =>
            loader.LoadAsync<byte[]>(new AssetRequest(url), cancellationToken);
    }
}
