using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SiPV.AssetLoader
{
    /// <summary>Decodes PNG or JPEG bytes into a <see cref="Texture2D"/>.</summary>
    /// <remarks>
    /// Matches <c>image/*</c> content types and the <c>png</c>, <c>jpg</c>, and <c>jpeg</c>
    /// extensions. <c>Texture2D.LoadImage</c> is a UnityEngine call and cannot run off the main
    /// thread, so this switches back before touching it: expect the decode to cost main-thread
    /// frame time proportional to image size. On unparseable bytes it destroys the partially
    /// created texture and throws rather than returning one with undefined contents.
    /// </remarks>
    public sealed class Texture2DDecoder : IAssetDecoder<Texture2D>
    {
        /// <inheritdoc />
        public bool CanDecode(Type targetType, string contentTypeOrExtension)
        {
            if (targetType != typeof(Texture2D) || string.IsNullOrEmpty(contentTypeOrExtension))
            {
                return false;
            }

            var value = contentTypeOrExtension.ToLowerInvariant();
            return value.StartsWith("image/") || value == "png" || value == "jpg" || value == "jpeg";
        }

        /// <inheritdoc />
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
