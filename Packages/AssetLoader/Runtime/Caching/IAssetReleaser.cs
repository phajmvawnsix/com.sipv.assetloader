namespace SiPV.AssetLoader
{
    /// <summary>
    /// Destroys an asset once the RAM cache evicts it and nothing holds a reference any more.
    /// </summary>
    /// <remarks>
    /// Implement this when your asset type needs teardown the default cannot know about, for
    /// example a prefab root whose child meshes and materials must also be destroyed, or a handle
    /// to an external native resource. Called on the main thread. Must tolerate being handed an
    /// asset that was already destroyed, and must not throw: an exception here would abort an
    /// eviction sweep partway through.
    /// </remarks>
    public interface IAssetReleaser
    {
        /// <summary>Destroys an evicted asset.</summary>
        /// <typeparam name="T">Asset type.</typeparam>
        /// <param name="asset">The asset to destroy. May be null or already destroyed.</param>
        void Release<T>(T asset);
    }
}
