using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Default IInFlightRequestCoordinator. One dictionary entry per ramKey currently loading,
    // boxed as object since the dictionary can't be generic per-entry. Main-thread-only per the
    // interface contract, so no lock needed for check-then-register.
    internal sealed class InFlightRequestCoordinator : IInFlightRequestCoordinator
    {
        private readonly Dictionary<string, object> _inFlight = new Dictionary<string, object>();

        public UniTaskCompletionSource<AssetHandle<T>> Register<T>(string ramKey)
        {
            var source = new UniTaskCompletionSource<AssetHandle<T>>();
            _inFlight[ramKey] = source;
            return source;
        }

        public bool TryGetExisting<T>(string ramKey, out UniTask<AssetHandle<T>> existing)
        {
            if (_inFlight.TryGetValue(ramKey, out var boxed) && boxed is UniTaskCompletionSource<AssetHandle<T>> source)
            {
                existing = source.Task;
                return true;
            }

            existing = default;
            return false;
        }

        public void Complete(string ramKey)
        {
            _inFlight.Remove(ramKey);
        }
    }
}
