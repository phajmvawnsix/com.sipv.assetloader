namespace SiPV.AssetLoader
{
    /// <summary>
    /// Derives the RAM and disk cache keys for a request. The place to implement cache-busting,
    /// for example folding an app or content version into every key so a release invalidates
    /// everything at once.
    /// </summary>
    /// <remarks>
    /// Called on every load including cache hits, so implementations must be cheap and
    /// deterministic: the same request has to produce the same keys every time or entries become
    /// unreachable.
    /// </remarks>
    public interface ICacheKeyProvider
    {
        /// <summary>Key for the in-memory tier.</summary>
        /// <param name="request">The request being loaded.</param>
        /// <returns>A dictionary key. No filesystem-safety requirement, so a readable raw string is fine and helps when debugging.</returns>
        string GetRamKey(in AssetRequest request);

        /// <summary>Key for the persistent tier.</summary>
        /// <param name="request">The request being loaded.</param>
        /// <returns>
        /// A string safe to use as a filename or path segment on every target platform. Hashing is
        /// the usual approach: raw URLs routinely exceed mobile path length limits and contain
        /// characters those filesystems reject.
        /// </returns>
        string GetDiskKey(in AssetRequest request);
    }
}
