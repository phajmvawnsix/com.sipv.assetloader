using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Non-generic base so the registry can store and match decoders without reflecting over closed
    /// generic types. Implement <see cref="IAssetDecoder{T}"/>, not this.
    /// </summary>
    public interface IAssetDecoder
    {
        /// <summary>Whether this decoder handles the given target type and content identifier.</summary>
        /// <param name="targetType">The type being requested. Return false for anything you do not produce.</param>
        /// <param name="contentTypeOrExtension">
        /// Either a MIME content type or a bare file extension (lowercase, no leading dot). The
        /// registry calls this separately for each, never both at once, so handle whichever forms
        /// you recognise and return false otherwise.
        /// </param>
        /// <returns>True when this decoder can handle the input.</returns>
        /// <remarks>Called during resolution on every cache-missing load, so keep it cheap: string comparisons, no I/O.</remarks>
        bool CanDecode(Type targetType, string contentTypeOrExtension);
    }

    /// <summary>
    /// Turns processed bytes into an asset of type <typeparamref name="T"/>. Implement and register
    /// via <see cref="AssetLoaderConfigBuilder.RegisterDecoder{T}"/> to support a new asset type
    /// without modifying this package.
    /// </summary>
    /// <typeparam name="T">The asset type produced.</typeparam>
    /// <remarks>
    /// Parsing and allocation may run on the thread pool, but every UnityEngine API call (creating
    /// a texture, an audio clip, a mesh) must happen on the main thread: switch with
    /// <c>UniTask.SwitchToMainThread()</c> first. The caller expects control back on the main
    /// thread when the returned task completes.
    /// </remarks>
    public interface IAssetDecoder<T> : IAssetDecoder
    {
        /// <summary>Decodes bytes into an asset.</summary>
        /// <param name="processedBytes">
        /// Bytes after the content processor chain has run, so already decrypted or decompressed if
        /// processors are registered.
        /// </param>
        /// <param name="context">URL, content type, extension, and the request's user data.</param>
        /// <param name="cancellationToken">Cancels the decode.</param>
        /// <returns>The decoded asset. Must complete on the main thread.</returns>
        /// <exception cref="AssetLoadException">
        /// Throw with <see cref="AssetLoadErrorCode.DecodeFailed"/> when the bytes cannot be
        /// decoded, rather than returning a half-built asset. Destroy anything already allocated
        /// before throwing, since nothing downstream will get a chance to.
        /// </exception>
        UniTask<T> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken);
    }
}
