using System;

namespace SiPV.AssetLoader
{
    // Ref-counted wrapper around a loaded asset. No finalizer cleanup - must call Release/Dispose or
    // the underlying UnityEngine.Object leaks in the RAM cache. All mutation (Retain/Release) and
    // RamCache access is confined to the main thread by contract, so no locking here - a
    // previous version had a lock alongside this same main-thread rule, which was worse than either
    // alone since it implied off-thread Release was supported. Double-release is a no-op that logs a
    // warning rather than throwing, so a stray extra Release doesn't crash a shipped build.
    public sealed class AssetHandle<T> : IDisposable
    {
        private sealed class SharedState
        {
            public T Asset;
            public string Key;
            public int RefCount;
            public Action<string> OnFullyReleased;
        }

        private readonly SharedState _state;
        private readonly IAssetLoaderLogger _logger;
        private bool _releasedByThisInstance;

        // onFullyReleased fires once, when ref count drops to 0, so the cache tier can evict.
        internal AssetHandle(string key, T asset, Action<string> onFullyReleased, IAssetLoaderLogger logger = null)
        {
            _state = new SharedState
            {
                Asset = asset,
                Key = key,
                RefCount = 1,
                OnFullyReleased = onFullyReleased
            };
            _logger = logger;
        }

        private AssetHandle(SharedState state, IAssetLoaderLogger logger)
        {
            _state = state;
            _logger = logger;
        }
        
        public T Asset
        {
            get
            {
                if (_releasedByThisInstance)
                {
                    throw new ObjectDisposedException(
                        $"AssetHandle<{typeof(T).Name}>", $"Handle for key '{_state.Key}' was already released.");
                }

                return _state.Asset;
            }
        }

        public string Key => _state.Key;

        public bool IsValid => _state.RefCount > 0;

        public int RefCount => _state.RefCount;

        // Bumps ref count, returns a new handle instance sharing the same count (each instance
        // released independently/once). Throws if already fully released - nothing left to retain.
        public AssetHandle<T> Retain()
        {
            if (_state.RefCount <= 0)
            {
                throw new InvalidOperationException(
                    $"Cannot retain AssetHandle<{typeof(T).Name}> for key '{_state.Key}': already fully released.");
            }

            _state.RefCount++;
            return new AssetHandle<T>(_state, _logger);
        }

        public void Release()
        {
            if (_releasedByThisInstance)
            {
                _logger?.LogWarning(
                    $"AssetHandle<{typeof(T).Name}> for key '{_state.Key}' released more than once by the same instance; ignored.");
                return;
            }

            _releasedByThisInstance = true;

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

        public void Dispose() => Release();
    }
}
