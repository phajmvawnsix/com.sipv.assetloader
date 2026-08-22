using System;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Static convenience wrapper around a single IAssetLoader instance, for projects without a DI
    /// container. Everything else in the package depends only on IAssetLoader, so tests and DI users
    /// can ignore this entirely. The concrete impl is AssetLoaderService, named differently so it
    /// doesn't collide with this class.
    /// </summary>
    public static class AssetLoader
    {
        private static IAssetLoader _default;

        // Throws if SetDefault was never called - no implicit default since default cache/source/decoder impls don't exist yet.
        public static IAssetLoader Default =>
            _default ?? throw new InvalidOperationException(
                $"{nameof(AssetLoader)}.{nameof(Default)} was read before {nameof(SetDefault)} was called. " +
                "Build an instance via AssetLoaderConfigBuilder and call AssetLoader.SetDefault(...) once at app bootstrap.");

        // Call once, from the main thread, at app bootstrap.
        public static void SetDefault(IAssetLoader instance)
        {
            _default = instance ?? throw new ArgumentNullException(nameof(instance));
        }
    }
}
