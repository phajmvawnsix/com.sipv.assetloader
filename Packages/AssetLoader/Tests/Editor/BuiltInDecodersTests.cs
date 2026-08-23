using System;
using System.Collections;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SiPV.AssetLoader.Tests
{
    public class BuiltInDecodersTests
    {
        private static AssetDecodeContext Context(string extension = "", string contentType = "") =>
            new AssetDecodeContext("http://example.com/asset", contentType, extension, null);

        [Test]
        public void ByteArrayDecoder_ReturnsBytesUnchanged()
        {
            var decoder = new ByteArrayDecoder();
            var bytes = new byte[] { 1, 2, 3 };

            var result = decoder.DecodeAsync(bytes, Context(), CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreSame(bytes, result);
        }

        [Test]
        public void ByteArrayDecoder_CanDecode_OnlyMatchesByteArrayType()
        {
            var decoder = new ByteArrayDecoder();

            Assert.IsTrue(decoder.CanDecode(typeof(byte[]), "anything"));
            Assert.IsFalse(decoder.CanDecode(typeof(string), "anything"));
        }

        [Test]
        public void StringDecoder_DecodesUtf8Bytes()
        {
            var decoder = new StringDecoder();
            var bytes = Encoding.UTF8.GetBytes("hello world");

            var result = decoder.DecodeAsync(bytes, Context(), CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("hello world", result);
        }

        [UnityTest]
        public IEnumerator Texture2DDecoder_ValidPngBytes_DecodesSuccessfully() => RunAsync(async () =>
        {
            var sourceTexture = new Texture2D(4, 4);
            var pngBytes = sourceTexture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(sourceTexture);

            var decoder = new Texture2DDecoder();
            var texture = await decoder.DecodeAsync(pngBytes, Context("png", "image/png"), CancellationToken.None);

            Assert.AreEqual(4, texture.width);
            Assert.AreEqual(4, texture.height);
            UnityEngine.Object.DestroyImmediate(texture);
        });

        [UnityTest]
        public IEnumerator Texture2DDecoder_GarbageBytes_ThrowsDecodeFailed() => RunAsync(async () =>
        {
            var decoder = new Texture2DDecoder();
            var garbage = new byte[] { 1, 2, 3, 4, 5 };

            AssetLoadException caught = null;
            try
            {
                await decoder.DecodeAsync(garbage, Context("png", "image/png"), CancellationToken.None);
            }
            catch (AssetLoadException ex)
            {
                caught = ex;
            }

            Assert.IsNotNull(caught);
            Assert.AreEqual(AssetLoadErrorCode.DecodeFailed, caught.ErrorCode);
        });

        [Test]
        public void Texture2DDecoder_CanDecode_MatchesImageContentTypeOrExtension()
        {
            var decoder = new Texture2DDecoder();

            Assert.IsTrue(decoder.CanDecode(typeof(Texture2D), "image/png"));
            Assert.IsTrue(decoder.CanDecode(typeof(Texture2D), "jpg"));
            Assert.IsFalse(decoder.CanDecode(typeof(Texture2D), "text/plain"));
            Assert.IsFalse(decoder.CanDecode(typeof(byte[]), "image/png"));
        }

        [UnityTest]
        public IEnumerator SpriteDecoder_WrapsTextureDecoder_ProducesMatchingSprite() => RunAsync(async () =>
        {
            var sourceTexture = new Texture2D(8, 8);
            var pngBytes = sourceTexture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(sourceTexture);

            var decoder = new SpriteDecoder(new Texture2DDecoder());
            var sprite = await decoder.DecodeAsync(pngBytes, Context("png", "image/png"), CancellationToken.None);

            Assert.AreEqual(8, sprite.texture.width);
            Assert.AreEqual(8, sprite.texture.height);
            UnityEngine.Object.DestroyImmediate(sprite.texture);
            UnityEngine.Object.DestroyImmediate(sprite);
        });

        [Test]
        public void SpriteDecoder_CanDecode_DelegatesToInnerTextureDecoder()
        {
            var decoder = new SpriteDecoder(new Texture2DDecoder());

            Assert.IsTrue(decoder.CanDecode(typeof(Sprite), "image/png"));
            Assert.IsFalse(decoder.CanDecode(typeof(Sprite), "text/plain"));
        }

        [Test]
        public void AssetBundleDecoder_CanDecode_MatchesKnownBundleExtensions()
        {
            var decoder = new AssetBundleDecoder();

            Assert.IsTrue(decoder.CanDecode(typeof(AssetBundle), "bundle"));
            Assert.IsTrue(decoder.CanDecode(typeof(AssetBundle), "assetbundle"));
            Assert.IsTrue(decoder.CanDecode(typeof(AssetBundle), "unity3d"));
            Assert.IsFalse(decoder.CanDecode(typeof(AssetBundle), "png"));
        }

        private static IEnumerator RunAsync(Func<UniTask> testBody) => testBody().ToCoroutine();
    }
}
