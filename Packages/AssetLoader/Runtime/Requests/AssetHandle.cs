using System;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Ref-counted ownership token for a loaded asset. The asset stays alive in the RAM cache for
    /// as long as at least one handle to it is unreleased.
    /// </summary>
    /// <typeparam name="T">The decoded asset type.</typeparam>
    /// <remarks>
    /// <para>
    /// Releasing is mandatory, not optional cleanup. There is no finalizer safety net: a
    /// <c>Texture2D</c> or <c>AudioClip</c> is backed by native memory the GC cannot reclaim on its
    /// own schedule, and calling Unity's destroy APIs from the finalizer thread is not supported.
    /// A handle that is never released pins its asset in the cache for the rest of the session.
    /// </para>
    /// <para>
    /// Implements <see cref="IDisposable"/>, so <c>using</c> is the idiomatic form:
    /// <code>
    /// using var handle = await loader.LoadAsync&lt;Texture2D&gt;(request);
    /// image.texture = handle.Asset;
    /// </code>
    /// Keep the handle alive as a field instead when the asset outlives the method that loaded it,
    /// and release it in <c>OnDestroy</c>.
    /// </para>
    /// <para>
    /// Every instance owns exactly one reference. <see cref="Retain"/> hands out an additional
    /// instance sharing the same count, so two systems can hold the same asset and release
    /// independently. Not thread-safe by design: create, retain, and release from the main thread
    /// only, matching the RAM cache it reports to.
    /// </para>
    /// </remarks>
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

        /// <summary>The loaded asset.</summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this particular instance was already released. Other instances sharing the
        /// same ref count are unaffected and can still read their own <c>Asset</c>.
        /// </exception>
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

        /// <summary>The RAM cache key this asset is stored under. Useful for logging and diagnostics.</summary>
        public string Key => _state.Key;

        /// <summary>True while at least one handle sharing this asset is still unreleased.</summary>
        public bool IsValid => _state.RefCount > 0;

        /// <summary>
        /// How many unreleased handles currently share this asset. Reflects real callers only: the
        /// cache does not hold a hidden reference of its own, so a value of 0 means nothing outside
        /// the cache is using the asset and it is eligible for eviction.
        /// </summary>
        public int RefCount => _state.RefCount;

        /// <summary>
        /// Takes an additional reference and returns a second handle sharing the same count. Use it
        /// when handing an asset to another system that will release on its own schedule.
        /// </summary>
        /// <returns>A new handle that must be released independently of this one.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the asset is already fully released, since there is no live reference left
        /// to share.
        /// </exception>
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

        /// <summary>
        /// Gives up this instance's reference. Once the last handle is released the asset becomes
        /// eligible for eviction; it is not destroyed immediately, so a later load of the same key
        /// can still reuse it until the cache actually trims.
        /// </summary>
        /// <remarks>
        /// Releasing the same instance twice is a no-op that logs a warning rather than throwing, so
        /// a stray extra release in a teardown path cannot crash a shipped build.
        /// </remarks>
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

        /// <summary>Calls <see cref="Release"/>, so handles work with <c>using</c>.</summary>
        public void Dispose() => Release();
    }
}
