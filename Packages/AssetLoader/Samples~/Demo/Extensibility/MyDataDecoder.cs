using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using SiPV.AssetLoader;

namespace SiPV.AssetLoader.Samples.Demo
{
    // Fake ".mydata" format, sole content is a UTF8 string. Stands in for a real binary
    // parser (fbx/usd/proprietary level format) - swap DecodeAsync's body for the real
    // parse call, everything else (CanDecode, registration) stays the same shape.
    public sealed class MyDataDecoder : IAssetDecoder<MyDataAsset>
    {
        public bool CanDecode(Type targetType, string contentTypeOrExtension) =>
            targetType == typeof(MyDataAsset) && contentTypeOrExtension == "mydata";

        public UniTask<MyDataAsset> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken) =>
            UniTask.FromResult(new MyDataAsset(Encoding.UTF8.GetString(processedBytes)));
    }
}
