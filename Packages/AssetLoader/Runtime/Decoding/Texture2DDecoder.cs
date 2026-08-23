using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SiPV.AssetLoader
{
    // Built-in decoder for Texture2D. Texture2D.LoadImage is a UnityEngine API call, must run on main thread -
    // the pipeline may start decoding off-thread but is required to bring this call back to main thread first.
    public sealed class Texture2DDecoder : IAssetDecoder<Texture2D>
    {
        public bool CanDecode(Type targetType, string contentTypeOrExtension)
        {
            if (targetType != typeof(Texture2D) || string.IsNullOrEmpty(contentTypeOrExtension))
            {
                return false;
            }

            var value = contentTypeOrExtension.ToLowerInvariant();
            return value.StartsWith("image/") || value == "png" || value == "jpg" || value == "jpeg";
        }

        public async UniTask<Texture2D> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);

            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(processedBytes))
            {
                // Destroy is a no-op-until-next-frame in play mode but logs an error outside it
                // (Editor tooling, EditMode tests) - DestroyImmediate is the correct call there.
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(texture);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                throw new AssetLoadException(AssetLoadErrorCode.DecodeFailed, context.Url, "Texture2D.LoadImage failed to parse the image bytes.");
            }

            return texture;
        }
    }
}
