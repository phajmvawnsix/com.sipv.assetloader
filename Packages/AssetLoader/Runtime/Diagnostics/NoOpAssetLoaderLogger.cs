namespace SiPV.AssetLoader
{
    /// <summary>Opt-out <see cref="IAssetLoaderLogger"/> that discards every message.</summary>
    /// <remarks>
    /// Use this to disable logging entirely; the default logger is
    /// <see cref="CustomLogAssetLoaderLogger"/>, not this one.
    /// </remarks>
    public sealed class NoOpAssetLoaderLogger : IAssetLoaderLogger
    {
        /// <summary>Shared singleton instance.</summary>
        public static readonly NoOpAssetLoaderLogger Instance = new NoOpAssetLoaderLogger();

        /// <inheritdoc />
        public void LogWarning(string message)
        {
        }

        /// <inheritdoc />
        public void LogError(string message)
        {
        }
    }
}
