using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace SiPV.AssetLoader.Tests
{
    // Partial-vs-all-waiter cancellation semantics for dedup coalescing - confirmed decision this
    // step: the shared fetch only actually cancels once every currently-interested caller has
    // cancelled their own token. See docs/step-07-dedup-policies.md for the full reasoning.
    public class DedupCancellationTests
    {
        [UnityTest]
        public IEnumerator PartialCancel_DoesNotAffectOtherWaiter() => RunAsync(async () =>
        {
            var harness = new PipelineTestHarness();
            harness.Source.ArmGate();
            harness.Source.ResultFactory = _ => PipelineTestHarness.Ok200("shared-value");
            var request = new AssetRequest("http://example.com/dedup-a.fake");

            using var ctsA = new CancellationTokenSource();
            using var ctsB = new CancellationTokenSource();

            var taskA = harness.Loader.LoadAsync<FakeAsset>(request, ctsA.Token);
            var taskB = harness.Loader.LoadAsync<FakeAsset>(request, ctsB.Token);

            ctsA.Cancel();
            harness.Source.ReleaseGate();

            await CatchAsync<OperationCanceledException>(async () => await taskA);
            using var handleB = await taskB;

            Assert.AreEqual(1, harness.Source.FetchCallCount, "A backing out must not restart or duplicate the fetch");
            Assert.AreEqual("shared-value", handleB.Asset.Content, "B must still receive the result normally");
        });

        [UnityTest]
        public IEnumerator AllCancel_ActuallyStopsTheSharedFetch() => RunAsync(async () =>
        {
            var harness = new PipelineTestHarness();
            harness.Source.ArmGate(); // never released - only cancellation can end this fetch
            var request = new AssetRequest("http://example.com/dedup-b.fake");

            using var ctsA = new CancellationTokenSource();
            using var ctsB = new CancellationTokenSource();

            var taskA = harness.Loader.LoadAsync<FakeAsset>(request, ctsA.Token);
            var taskB = harness.Loader.LoadAsync<FakeAsset>(request, ctsB.Token);

            ctsA.Cancel();
            ctsB.Cancel();

            await CatchAsync<OperationCanceledException>(async () => await taskA);
            await CatchAsync<OperationCanceledException>(async () => await taskB);

            // coordinator must have cleaned up correctly - a fresh load of the same key must not be
            // stuck waiting on the now-abandoned in-flight entry
            harness.Source.ResultFactory = _ => PipelineTestHarness.Ok200("recovered");
            using var handle = await harness.Loader.LoadAsync<FakeAsset>(request);
            Assert.AreEqual("recovered", handle.Asset.Content);
        });

        [UnityTest]
        public IEnumerator UncancellableWaiter_PreventsAutoCancel_EvenIfOthersCancel() => RunAsync(async () =>
        {
            var harness = new PipelineTestHarness();
            harness.Source.ArmGate();
            harness.Source.ResultFactory = _ => PipelineTestHarness.Ok200("still-comes-through");
            var request = new AssetRequest("http://example.com/dedup-c.fake");

            using var ctsA = new CancellationTokenSource();

            var taskA = harness.Loader.LoadAsync<FakeAsset>(request, ctsA.Token);
            var taskB = harness.Loader.LoadAsync<FakeAsset>(request); // default token - can never cancel

            ctsA.Cancel();
            await CatchAsync<OperationCanceledException>(async () => await taskA);

            // B never cancels and can't - the shared fetch must still be running for it
            harness.Source.ReleaseGate();
            using var handleB = await taskB;

            Assert.AreEqual("still-comes-through", handleB.Asset.Content);
            Assert.AreEqual(1, harness.Source.FetchCallCount);
        });

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
