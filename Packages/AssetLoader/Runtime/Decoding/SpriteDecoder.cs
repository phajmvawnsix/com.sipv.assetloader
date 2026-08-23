using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SiPV.AssetLoader
{
    // Built-in decoder for Sprite.
    public sealed class SpriteDecoder : IAssetDecoder<Sprite>
    {
        private readonly IAssetDecoder<Texture2D> _textureDecoder;

        public SpriteDecoder(IAssetDecoder<Texture2D> textureDecoder)
        {
            _textureDecoder = textureDecoder ?? throw new ArgumentNullException(nameof(textureDecoder));
        }

        public bool CanDecode(Type targetType, string contentTypeOrExtension) =>
            targetType == typeof(Sprite) && _textureDecoder.CanDecode(typeof(Texture2D), contentTypeOrExtension);

        public async UniTask<Sprite> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken)
        {
            var texture = await _textureDecoder.DecodeAsync(processedBytes, context, cancellationToken);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}
