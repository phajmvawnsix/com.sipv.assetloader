using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace SiPV.AssetLoader.Tests
{
    public class AssetDecoderRegistryTests
    {
        [Test]
        public void TryResolve_ByContentType_ReturnsRegisteredDecoder()
        {
            var registry = new AssetDecoderRegistry();
            var decoder = new FakeAssetDecoder();
            registry.Register(decoder);

            var found = registry.TryResolve<FakeAsset>(FakeAssetDecoder.ContentType, "somethingelse", out var resolved);

            Assert.IsTrue(found);
            Assert.AreSame(decoder, resolved);
        }

        [Test]
        public void TryResolve_ByExtension_ReturnsRegisteredDecoder()
        {
            var registry = new AssetDecoderRegistry();
            var decoder = new FakeAssetDecoder();
            registry.Register(decoder);

            var found = registry.TryResolve<FakeAsset>("somethingelse", "fake", out var resolved);

            Assert.IsTrue(found);
            Assert.AreSame(decoder, resolved);
        }

        [Test]
        public void TryResolve_UnregisteredType_ReturnsFalse()
        {
            var registry = new AssetDecoderRegistry();

            var found = registry.TryResolve<FakeAsset>(FakeAssetDecoder.ContentType, "fake", out var resolved);

            Assert.IsFalse(found);
            Assert.IsNull(resolved);
        }

        [Test]
        public void TryResolve_NoCandidateMatchesContentTypeOrExtension_ReturnsFalse()
        {
            var registry = new AssetDecoderRegistry();
            registry.Register(new FakeAssetDecoder());

            var found = registry.TryResolve<FakeAsset>("text/unrelated", "unrelated", out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void Register_LaterRegistration_WinsOverEarlierOnConflict()
        {
            var registry = new AssetDecoderRegistry();
            var first = new FakeAssetDecoder();
            var second = new FakeAssetDecoder();
            registry.Register(first);
            registry.Register(second);

            registry.TryResolve<FakeAsset>(FakeAssetDecoder.ContentType, "fake", out var resolved);

            Assert.AreSame(second, resolved, "last-registered decoder must win on conflict");
        }

        [Test]
        public void Register_NullDecoder_Throws()
        {
            var registry = new AssetDecoderRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Register<FakeAsset>(null));
        }

        // Proves Open/Closed: a decoder for a type the Runtime asmdef has never seen, defined and
        // registered entirely from this Tests assembly, resolves and decodes correctly. No package
        // code was touched to support MadeUpTestAsset.
        private sealed class MadeUpTestAsset
        {
            public int Value { get; }
            public MadeUpTestAsset(int value) => Value = value;
        }

        private sealed class MadeUpTestAssetDecoder : IAssetDecoder<MadeUpTestAsset>
        {
            public bool CanDecode(Type targetType, string contentTypeOrExtension) =>
                targetType == typeof(MadeUpTestAsset) && contentTypeOrExtension == "madeup";

            public UniTask<MadeUpTestAsset> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken) =>
                UniTask.FromResult(new MadeUpTestAsset(BitConverter.ToInt32(processedBytes, 0)));
        }

        [Test]
        public void CustomDecoderDefinedOutsideRuntimeAssembly_ResolvesAndDecodes()
        {
            var registry = new AssetDecoderRegistry();
            registry.Register(new MadeUpTestAssetDecoder());

            var found = registry.TryResolve<MadeUpTestAsset>("madeup", "madeup", out var decoder);
            Assert.IsTrue(found);

            var result = decoder.DecodeAsync(BitConverter.GetBytes(42), default, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert.AreEqual(42, result.Value);
        }
    }
}
