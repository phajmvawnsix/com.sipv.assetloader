namespace SiPV.AssetLoader
{
    // Default IAssetLoaderLogger - discards everything.
    public sealed class NoOpAssetLoaderLogger : IAssetLoaderLogger
    {
        public static readonly NoOpAssetLoaderLogger Instance = new NoOpAssetLoaderLogger();

        public void LogWarning(string message)
        {
        }

        public void LogError(string message)
        {
        }
    }
}
