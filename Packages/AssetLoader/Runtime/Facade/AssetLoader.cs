using System;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Static convenience wrapper around a single <see cref="IAssetLoader"/> instance, for projects
    /// without a DI container.
    /// </summary>
    /// <remarks>
    /// Nothing else in the package references this type: the pipeline, caches, and policies all
    /// depend only on <see cref="IAssetLoader"/>, so tests and DI-based projects can ignore it
    /// entirely and there is no hidden static coupling. The concrete implementation is
    /// <see cref="AssetLoaderService"/>, named differently so it does not collide with this class.
    /// </remarks>
    public static class AssetLoader
    {
        private static IAssetLoader _default;

        /// <summary>The instance registered by <see cref="SetDefault"/>.</summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when read before <see cref="SetDefault"/> was called. There is no implicit
        /// fallback instance on purpose: a loader needs a cache directory and budget decisions only
        /// the host project can make. Use <see cref="AssetLoaderConfigBuilder.CreateDefault"/> to
        /// get a fully wired builder in one call if you have no particular preference.
        /// </exception>
        public static IAssetLoader Default =>
            _default ?? throw new InvalidOperationException(
                $"{nameof(AssetLoader)}.{nameof(Default)} was read before {nameof(SetDefault)} was called. " +
                "Build an instance via AssetLoaderConfigBuilder and call AssetLoader.SetDefault(...) once at app bootstrap.");

        /// <summary>Registers the instance returned by <see cref="Default"/>.</summary>
        /// <param name="instance">The loader to expose globally.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="instance"/> is null.</exception>
        /// <remarks>
        /// Call once, from the main thread, at app bootstrap. Calling it again replaces the
        /// instance but neither disposes nor drains the previous one, so handles already handed out
        /// by the old loader still belong to the old loader's caches.
        /// </remarks>
        public static void SetDefault(IAssetLoader instance)
        {
            _default = instance ?? throw new ArgumentNullException(nameof(instance));
        }
    }
}
