using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>Coalesces concurrent loads of the same RAM key into one fetch, so 1 URL produces 1 network round trip.</summary>
    /// <remarks>
    /// Main-thread-only by contract: every method must be called from the main thread, which is
    /// what lets check-then-register be race-free without locking.
    /// </remarks>
    public interface IInFlightRequestCoordinator
    {
        /// <summary>
        /// Marks <paramref name="ramKey"/> as in-flight. The caller resolves the returned source
        /// once the pipeline finishes. Only call this after <see cref="TryGetExisting{T}"/> has
        /// confirmed nothing is already registered for the key.
        /// </summary>
        /// <param name="ramKey">The RAM cache key being loaded.</param>
        /// <param name="callerToken">This caller's own cancellation token.</param>
        /// <param name="sharedToken">
        /// A token that only cancels once every waiter's <paramref name="callerToken"/> has
        /// cancelled, since one caller giving up must not abort the shared fetch for the others.
        /// </param>
        UniTaskCompletionSource<AssetHandle<T>> Register<T>(string ramKey, CancellationToken callerToken, out CancellationToken sharedToken);

        /// <summary>Checks whether a fetch for <paramref name="ramKey"/> is already in flight and, if so, returns a task that resolves alongside it.</summary>
        /// <param name="ramKey">The RAM cache key to check.</param>
        /// <param name="callerToken">This caller's own cancellation token, attached to the returned task.</param>
        /// <param name="existing">The shared in-flight task, when one exists.</param>
        /// <returns>True when an in-flight fetch was found and coalesced onto.</returns>
        bool TryGetExisting<T>(string ramKey, CancellationToken callerToken, out UniTask<AssetHandle<T>> existing);

        /// <summary>
        /// Unregisters <paramref name="ramKey"/> as in-flight and resolves every coalesced waiter.
        /// </summary>
        /// <remarks>
        /// Must run in a <c>finally</c>, including on the failure path: skipping it on an
        /// exception leaves the key wedged as in-flight forever, and every later load of it
        /// awaits a source that never resolves.
        /// </remarks>
        void Complete<T>(string ramKey, UniTaskCompletionSource<AssetHandle<T>> completionSource);
    }
}
