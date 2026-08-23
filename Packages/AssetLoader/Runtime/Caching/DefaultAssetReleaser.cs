using UnityEngine;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Destroys <c>UnityEngine.Object</c> assets and leaves everything else to the garbage
    /// collector.
    /// </summary>
    /// <remarks>
    /// Textures, audio clips, and other Unity objects hold native memory the GC cannot reclaim, so
    /// they get an explicit <c>Object.Destroy</c>. Plain managed types (strings, byte arrays,
    /// POCOs) need nothing: dropping the cache's reference is enough. Composite assets whose
    /// children also need destroying want a custom <see cref="IAssetReleaser"/>, since destroying
    /// only the root would leak the rest.
    /// </remarks>
    public sealed class DefaultAssetReleaser : IAssetReleaser
    {
        /// <inheritdoc />
        public void Release<T>(T asset)
        {
            if (asset is Object unityObject && unityObject != null)
            {
                Object.Destroy(unityObject);
            }
        }
    }
}
