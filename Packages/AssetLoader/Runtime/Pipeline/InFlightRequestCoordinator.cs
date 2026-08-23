using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Default IInFlightRequestCoordinator. One dictionary entry per ramKey currently loading,
    // boxed as object since the dictionary can't be generic per-entry. Main-thread-only per the
    // interface contract, so no lock needed for check-then-register.
    internal sealed class InFlightRequestCoordinator : IInFlightRequestCoordinator
    {
        private sealed class Entry
        {
            public object CompletionSource;
            public CancellationTokenSource SharedCts;
            public int ActiveParticipants;
            public bool HasUncancellableParticipant;
            public readonly List<CancellationTokenRegistration> Registrations = new List<CancellationTokenRegistration>();
        }

        private readonly Dictionary<string, Entry> _inFlight = new Dictionary<string, Entry>();

        public UniTaskCompletionSource<AssetHandle<T>> Register<T>(string ramKey, CancellationToken callerToken, out CancellationToken sharedToken)
        {
            var source = new UniTaskCompletionSource<AssetHandle<T>>();
            var entry = new Entry
            {
                CompletionSource = source,
                SharedCts = new CancellationTokenSource()
            };

            _inFlight[ramKey] = entry;
            Join(entry, callerToken);

            sharedToken = entry.SharedCts.Token;
            return source;
        }

        public bool TryGetExisting<T>(string ramKey, CancellationToken callerToken, out UniTask<AssetHandle<T>> existing)
        {
            if (_inFlight.TryGetValue(ramKey, out var entry) && entry.CompletionSource is UniTaskCompletionSource<AssetHandle<T>> source)
            {
                Join(entry, callerToken);
                existing = source.Task;
                return true;
            }

            existing = default;
            return false;
        }

        public void Complete(string ramKey)
        {
            if (!_inFlight.TryGetValue(ramKey, out var entry))
            {
                return;
            }

            _inFlight.Remove(ramKey);
            
            foreach (var registration in entry.Registrations)
            {
                registration.Dispose();
            }

            entry.SharedCts.Dispose();
        }

        private static void Join(Entry entry, CancellationToken callerToken)
        {
            if (!callerToken.CanBeCanceled)
            {
                entry.HasUncancellableParticipant = true;
                return;
            }

            Interlocked.Increment(ref entry.ActiveParticipants);
            entry.Registrations.Add(callerToken.Register(() =>
            {
                if (Interlocked.Decrement(ref entry.ActiveParticipants) <= 0 && !entry.HasUncancellableParticipant)
                {
                    entry.SharedCts.Cancel();
                }
            }));
        }
    }
}
