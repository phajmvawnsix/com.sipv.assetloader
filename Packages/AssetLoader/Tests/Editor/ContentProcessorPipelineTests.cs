using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace SiPV.AssetLoader.Tests
{
    public class ContentProcessorPipelineTests
    {
        // Appends a marker byte so test assertions can see exactly which processors ran and in
        // what order, rather than just trusting the final byte count.
        private sealed class AppendByteProcessor : IContentProcessor
        {
            private readonly byte _marker;
            public AppendByteProcessor(byte marker) => _marker = marker;

            public UniTask<byte[]> ProcessAsync(byte[] input, AssetProcessingContext context, CancellationToken cancellationToken)
            {
                var output = new byte[input.Length + 1];
                Array.Copy(input, output, input.Length);
                output[input.Length] = _marker;
                return UniTask.FromResult(output);
            }
        }

        private sealed class XorProcessor : IContentProcessor
        {
            private readonly byte _key;
            public XorProcessor(byte key) => _key = key;

            public UniTask<byte[]> ProcessAsync(byte[] input, AssetProcessingContext context, CancellationToken cancellationToken)
            {
                var output = new byte[input.Length];
                for (var i = 0; i < input.Length; i++)
                {
                    output[i] = (byte)(input[i] ^ _key);
                }
                return UniTask.FromResult(output);
            }
        }

        private static AssetProcessingContext Context() => new AssetProcessingContext("http://example.com/a", "application/octet-stream", null);

        [UnityTest]
        public IEnumerator EmptyChain_ReturnsInputUnchanged() => RunAsync(async () =>
        {
            var pipeline = new ContentProcessorPipeline(Array.Empty<IContentProcessor>());
            var input = new byte[] { 1, 2, 3 };

            var result = await pipeline.RunAsync(input, Context(), CancellationToken.None);

            CollectionAssert.AreEqual(input, result);
        });

        [UnityTest]
        public IEnumerator MultipleProcessors_RunInRegistrationOrder() => RunAsync(async () =>
        {
            var pipeline = new ContentProcessorPipeline(new List<IContentProcessor>
            {
                new AppendByteProcessor(1),
                new AppendByteProcessor(2),
                new AppendByteProcessor(3),
            });

            var result = await pipeline.RunAsync(Array.Empty<byte>(), Context(), CancellationToken.None);

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, result);
        });

        [UnityTest]
        public IEnumerator XorProcessor_AppliedTwiceWithSameKey_RoundTripsToOriginal() => RunAsync(async () =>
        {
            // Same shape as Samples~/Demo/Extensibility/XorContentProcessor.cs (Samples~ is
            // ignored by Unity's asset database due to the trailing "~", so it can't be referenced
            // by an asmdef here - this proves the algorithm the sample documents actually round-trips).
            var original = System.Text.Encoding.UTF8.GetBytes("round trip me");
            var pipeline = new ContentProcessorPipeline(new List<IContentProcessor> { new XorProcessor(0x5A) });

            var encoded = await pipeline.RunAsync(original, Context(), CancellationToken.None);
            var decoded = await pipeline.RunAsync(encoded, Context(), CancellationToken.None);

            CollectionAssert.AreNotEqual(original, encoded);
            CollectionAssert.AreEqual(original, decoded);
        });

        [UnityTest]
        public IEnumerator Cancellation_StopsBeforeNextProcessor() => RunAsync(async () =>
        {
            using var cts = new CancellationTokenSource();
            var ranAfterCancel = false;
            var pipeline = new ContentProcessorPipeline(new List<IContentProcessor>
            {
                new AppendByteProcessor(1),
                new CancelSelfProcessor(cts),
                new MarkRanProcessor(() => ranAfterCancel = true),
            });

            await CatchAsync<OperationCanceledException>(async () =>
                await pipeline.RunAsync(Array.Empty<byte>(), Context(), cts.Token));

            Assert.IsFalse(ranAfterCancel, "a processor after the cancelled one must never run");
        });

        // Cancels the token from inside its own ProcessAsync, so the NEXT loop iteration's
        // ThrowIfCancellationRequested is what actually stops the chain.
        private sealed class CancelSelfProcessor : IContentProcessor
        {
            private readonly CancellationTokenSource _cts;
            public CancelSelfProcessor(CancellationTokenSource cts) => _cts = cts;

            public UniTask<byte[]> ProcessAsync(byte[] input, AssetProcessingContext context, CancellationToken cancellationToken)
            {
                _cts.Cancel();
                return UniTask.FromResult(input);
            }
        }

        private sealed class MarkRanProcessor : IContentProcessor
        {
            private readonly Action _onRan;
            public MarkRanProcessor(Action onRan) => _onRan = onRan;

            public UniTask<byte[]> ProcessAsync(byte[] input, AssetProcessingContext context, CancellationToken cancellationToken)
            {
                _onRan();
                return UniTask.FromResult(input);
            }
        }

        private static IEnumerator RunAsync(Func<UniTask> testBody) => testBody().ToCoroutine();

        private static async System.Threading.Tasks.Task<TException> CatchAsync<TException>(Func<System.Threading.Tasks.Task> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException ex)
            {
                return ex;
            }

            Assert.Fail($"Expected {typeof(TException).Name} to be thrown, but nothing was.");
            return null;
        }
    }
}
