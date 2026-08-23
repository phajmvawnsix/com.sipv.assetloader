using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Built-in decoder for byte[]: the processed bytes ARE the asset, no transform needed.
    public sealed class ByteArrayDecoder : IAssetDecoder<byte[]>
    {
        public bool CanDecode(Type targetType, string contentTypeOrExtension) => targetType == typeof(byte[]);

        public UniTask<byte[]> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken) =>
            UniTask.FromResult(processedBytes);
    }
}
