using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SiPV.AssetLoader;

namespace SiPV.AssetLoader.Samples.Demo
{
    // Deliberately the simplest possible transform, to keep the sample about the
    // registration pattern rather than about cryptography. XOR is symmetric, so this
    // one processor doubles as both the encrypt step (offline, when you produce the
    // cached bytes) and the decrypt step (here, when the pipeline reads them back).
    // A real production project should use AES (or whatever your platform's crypto
    // library provides) instead - XOR-with-a-fixed-key is trivially reversible by
    // anyone who extracts the app.
    public sealed class XorContentProcessor : IContentProcessor
    {
        private readonly byte[] _key;

        public XorContentProcessor(byte[] key)
        {
            if (key == null || key.Length == 0)
            {
                throw new ArgumentException("Key must be non-null and non-empty.", nameof(key));
            }

            _key = key;
        }

        public UniTask<byte[]> ProcessAsync(byte[] input, AssetProcessingContext context, CancellationToken cancellationToken)
        {
            var output = new byte[input.Length];
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (byte)(input[i] ^ _key[i % _key.Length]);
            }

            return UniTask.FromResult(output);
        }
    }
}
