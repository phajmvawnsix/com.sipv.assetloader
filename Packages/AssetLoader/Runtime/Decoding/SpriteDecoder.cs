using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SiPV.AssetLoader
{
    /// <summary>Decodes image bytes into a <see cref="Sprite"/> by wrapping a texture decoder.</summary>
    /// <remarks>
    /// <para>
    /// Not a from-scratch image parser: it delegates to an <c>IAssetDecoder&lt;Texture2D&gt;</c> and
    /// wraps the result with <c>Sprite.Create</c>, so it accepts whatever formats that decoder
    /// accepts and inherits its main-thread requirement.
    /// </para>
    /// <para>
    /// The sprite uses a full-rect, centred pivot at the default pixels-per-unit.
    /// <see cref="AssetDecodeContext"/> carries no field for those, so register your own
    /// <c>IAssetDecoder&lt;Sprite&gt;</c> if you need different values: last registration wins, so
    /// yours will take precedence over this one.
    /// </para>
    /// <para>
    /// The sprite and its texture are two separate Unity objects. Destroying only the sprite leaks
    /// the texture, which is why a custom <see cref="IAssetReleaser"/> is worth registering if you
    /// cache sprites heavily.
    /// </para>
    /// </remarks>
    public sealed class SpriteDecoder : IAssetDecoder<Sprite>
    {
        private readonly IAssetDecoder<Texture2D> _textureDecoder;

        /// <summary>Creates a sprite decoder over an existing texture decoder.</summary>
        /// <param name="textureDecoder">
        /// Does the actual image decoding. Sharing one instance with your registered
        /// <c>IAssetDecoder&lt;Texture2D&gt;</c> is fine and avoids a redundant second object.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="textureDecoder"/> is null.</exception>
        public SpriteDecoder(IAssetDecoder<Texture2D> textureDecoder)
        {
            _textureDecoder = textureDecoder ?? throw new ArgumentNullException(nameof(textureDecoder));
        }

        /// <inheritdoc />
        public bool CanDecode(Type targetType, string contentTypeOrExtension) =>
            targetType == typeof(Sprite) && _textureDecoder.CanDecode(typeof(Texture2D), contentTypeOrExtension);

        /// <inheritdoc />
        public async UniTask<Sprite> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken)
        {
            // No SwitchToMainThread here: the inner decoder already guarantees it returns on the
            // main thread, and adding a second one would open a cancellable await window between
            // the texture being allocated and Sprite.Create running, leaking the texture if
            // cancellation landed in it.
            var texture = await _textureDecoder.DecodeAsync(processedBytes, context, cancellationToken);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}
