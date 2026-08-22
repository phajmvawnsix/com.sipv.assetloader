using System;

namespace SiPV.AssetLoader
{
    // Ref-counted wrapper around a loaded asset. No finalizer cleanup - must call Release/Dispose or the
    // underlying UnityEngine.Object leaks in the RAM cache. Release must happen on the main thread only:
    // hitting zero calls back into IRamCache's eviction, which isn't thread-safe. Double-release is a
    // no-op (not an exception), kept defensive so a stray double-release doesn't crash the app.
    // TODO: the Gate lock is belt-and-braces given the main-thread rule above - either drop it or drop the
    // rule, having both just makes people think off-thread Release is supported.
    public sealed class AssetHandle<T> : IDisposable
    {
        private sealed class SharedState
        {
            public T Asset;
            public string Key;
            public int RefCount;
            public Action<string> OnFullyReleased;
            public readonly object Gate = new object();
        }

        private readonly SharedState _state;
        private bool _releasedByThisInstance;

        // onFullyReleased fires once, when ref count drops to 0, so the cache tier can evict.
        internal AssetHandle(string key, T asset, Action<string> onFullyReleased)
        {
            _state = new SharedState
            {
                Asset = asset,
                Key = key,
                RefCount = 1,
                OnFullyReleased = onFullyReleased
            };
        }

        private AssetHandle(SharedState state)
        {
            _state = state;
        }

        // Only safe to touch while IsValid is true.
        public T Asset => _state.Asset;

        public string Key => _state.Key;

        public bool IsValid
        {
            get { lock (_state.Gate) return _state.RefCount > 0; }
        }

        public int RefCount
        {
            get { lock (_state.Gate) return _state.RefCount; }
        }

        // Bumps ref count, returns a new handle instance sharing the same count (each instance
        // released independently/once). Throws if already fully released - nothing left to retain.
        public AssetHandle<T> Retain()
        {
            lock (_state.Gate)
            {
                if (_state.RefCount <= 0)
                {
                    throw new InvalidOperationException(
                        $"Cannot retain AssetHandle<{typeof(T).Name}> for key '{_state.Key}': already fully released.");
                }

                _state.RefCount++;
            }

            return new AssetHandle<T>(_state);
        }

        public void Release()
        {
            if (_releasedByThisInstance)
            {
                return;
            }

            _releasedByThisInstance = true;

            lock (_state.Gate)
            {
                if (_state.RefCount <= 0)
                {
                    return;
                }

                _state.RefCount--;

                if (_state.RefCount == 0)
                {
                    _state.OnFullyReleased?.Invoke(_state.Key);
                }
            }
        }

        public void Dispose() => Release();
    }
}
