namespace SiPV.AssetLoader
{
    /// <summary>
    /// Estimates how much memory a decoded asset occupies, so the RAM cache can enforce a byte
    /// budget.
    /// </summary>
    /// <remarks>
    /// Implement this when caching a type the built-in estimator cannot size meaningfully, for
    /// example a composite prefab, and an accurate byte budget matters. Called on the main thread
    /// while storing an asset, so keep it cheap: a rough estimate that runs in constant time beats
    /// an exact one that walks the object graph.
    /// </remarks>
    public interface IMemorySizeEstimator
    {
        /// <summary>Approximate resident size of an asset.</summary>
        /// <typeparam name="T">Asset type.</typeparam>
        /// <param name="asset">The asset to size. May be null.</param>
        /// <returns>
        /// Estimated bytes. Return a small non-zero value rather than 0 for types you cannot size,
        /// so they still count toward an entry-count budget without distorting a byte budget.
        /// </returns>
        long EstimateBytes<T>(T asset);
    }
}
