using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Built-in decoder for string: UTF8 text. No BOM handling beyond what Encoding.UTF8 already does.
    public sealed class StringDecoder : IAssetDecoder<string>
    {
        public bool CanDecode(Type targetType, string contentTypeOrExtension) => targetType == typeof(string);

        public UniTask<string> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken) =>
            UniTask.FromResult(Encoding.UTF8.GetString(processedBytes));
    }
}
