using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Passthrough decoder for raw bytes: the processed bytes are the asset, nothing to transform.
    /// </summary>
    /// <remarks>
    /// Register this to load formats you parse yourself. Matches on the target type alone, so it
    /// accepts any content type or extension. The returned array is the pipeline's own buffer, not
    /// a copy, so treat it as read-only: mutating it corrupts the cached entry other callers see.
    /// </remarks>
    public sealed class ByteArrayDecoder : IAssetDecoder<byte[]>
    {
        /// <inheritdoc />
        public bool CanDecode(Type targetType, string contentTypeOrExtension) => targetType == typeof(byte[]);

        /// <inheritdoc />
        public UniTask<byte[]> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken) =>
            UniTask.FromResult(processedBytes);
    }
}
