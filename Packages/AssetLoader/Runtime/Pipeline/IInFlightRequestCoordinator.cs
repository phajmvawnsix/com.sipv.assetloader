using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Coalesces concurrent loads of the same RAM key into one fetch (1 URL = 1 network round-trip).
    // Main-thread-only by contract, so no locking needed for check-then-register.
    public interface IInFlightRequestCoordinator
    {
        // Marks ramKey as in-flight; caller resolves the returned source when the pipeline finishes.
        // Only call after TryGetExisting confirms nothing's already registered.
        UniTaskCompletionSource<AssetHandle<T>> Register<T>(string ramKey);

        bool TryGetExisting<T>(string ramKey, out UniTask<AssetHandle<T>> existing);

        // Must run in a finally, including on the failure path, or the key stays wedged as in-flight
        // and every later load of it awaits a source that never resolves.
        void Complete(string ramKey);
    }
}
