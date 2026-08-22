using UnityEngine;

namespace SiPV.AssetLoader
{
    // Default IAssetReleaser. UnityEngine.Object types (Texture2D, AudioClip, ...) get
    // Object.Destroy on the main thread. Anything else (string, byte[], plain POCOs) has nothing
    // to release explicitly - the GC handles it once the cache drops its reference.
    public sealed class DefaultAssetReleaser : IAssetReleaser
    {
        public void Release<T>(T asset)
        {
            if (asset is Object unityObject && unityObject != null)
            {
                Object.Destroy(unityObject);
            }
        }
    }
}
