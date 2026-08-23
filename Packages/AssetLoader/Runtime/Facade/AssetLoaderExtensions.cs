using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Typed shorthand over <see cref="IAssetLoader.LoadAsync{T}"/> for the common asset types.
    /// </summary>
    /// <remarks>
    /// These are one-line forwards, not a parallel API. Adding support for a new asset type never
    /// requires adding a method here: register a decoder and call
    /// <see cref="IAssetLoader.LoadAsync{T}"/> with your own type. Use the generic call directly
    /// whenever you need per-request policy overrides, a custom cache key, or request flags, since
    /// these overloads only take a URL.
    /// </remarks>
    public static class AssetLoaderExtensions
    {
        /// <summary>Loads a texture. Requires a registered <c>IAssetDecoder&lt;Texture2D&gt;</c>.</summary>
        /// <param name="loader">The loader to call.</param>
        /// <param name="url">Asset URL. Doubles as the cache key.</param>
        /// <param name="cancellationToken">Cancels this caller's wait.</param>
        /// <returns>A ref-counted handle the caller must release when done.</returns>
        public static UniTask<AssetHandle<UnityEngine.Texture2D>> LoadTextureAsync(this IAssetLoader loader, string url, CancellationToken cancellationToken = default) =>
            loader.LoadAsync<UnityEngine.Texture2D>(new AssetRequest(url), cancellationToken);

        /// <summary>Loads an audio clip. Requires a registered <c>IAssetDecoder&lt;AudioClip&gt;</c>.</summary>
        /// <param name="loader">The loader to call.</param>
        /// <param name="url">Asset URL. Doubles as the cache key.</param>
        /// <param name="cancellationToken">Cancels this caller's wait.</param>
        /// <returns>A ref-counted handle the caller must release when done.</returns>
        public static UniTask<AssetHandle<UnityEngine.AudioClip>> LoadAudioClipAsync(this IAssetLoader loader, string url, CancellationToken cancellationToken = default) =>
            loader.LoadAsync<UnityEngine.AudioClip>(new AssetRequest(url), cancellationToken);

        /// <summary>Loads UTF8 text. Requires a registered <c>IAssetDecoder&lt;string&gt;</c>.</summary>
        /// <param name="loader">The loader to call.</param>
        /// <param name="url">Asset URL. Doubles as the cache key.</param>
        /// <param name="cancellationToken">Cancels this caller's wait.</param>
        /// <returns>A ref-counted handle the caller must release when done.</returns>
        public static UniTask<AssetHandle<string>> LoadTextAsync(this IAssetLoader loader, string url, CancellationToken cancellationToken = default) =>
            loader.LoadAsync<string>(new AssetRequest(url), cancellationToken);

        /// <summary>
        /// Loads raw bytes, for formats you decode yourself rather than through a registered
        /// decoder. Requires a registered <c>IAssetDecoder&lt;byte[]&gt;</c>, which is a passthrough.
        /// </summary>
        /// <param name="loader">The loader to call.</param>
        /// <param name="url">Asset URL. Doubles as the cache key.</param>
        /// <param name="cancellationToken">Cancels this caller's wait.</param>
        /// <returns>A ref-counted handle the caller must release when done.</returns>
        public static UniTask<AssetHandle<byte[]>> LoadBytesAsync(this IAssetLoader loader, string url, CancellationToken cancellationToken = default) =>
            loader.LoadAsync<byte[]>(new AssetRequest(url), cancellationToken);
    }
}
