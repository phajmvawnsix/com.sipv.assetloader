using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Non-generic base so AssetDecoderRegistry can store/look up decoders without reflecting over the closed generic type.
    public interface IAssetDecoder
    {
        // caller passes contentType or extension, not both
        bool CanDecode(Type targetType, string contentTypeOrExtension);
    }

    /// <summary>Decodes already content-processed bytes into T. Implement + register via AssetLoaderConfigBuilder.RegisterDecoder to support a new asset type.</summary>
    public interface IAssetDecoder<T> : IAssetDecoder
    {
        // Parsing/allocation can run on the thread pool, but any UnityEngine API call (Texture2D.LoadImage,
        // new AudioClip, etc) must switch to main thread first - caller expects control back on main thread when this completes.
        UniTask<T> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken);
    }
}
