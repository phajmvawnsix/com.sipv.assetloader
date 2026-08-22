using SiPVLib.Debugging;

namespace SiPV.AssetLoader
{
    // Routes through SiPVLib's CustomLog (com.sipvlib.debugging), which wraps UnityEngine.Debug and
    // can be silenced globally via LOGGING_DISABLE. Use NoOpAssetLoaderLogger to silence just this package.
    public sealed class CustomLogAssetLoaderLogger : IAssetLoaderLogger
    {
        public static readonly CustomLogAssetLoaderLogger Instance = new CustomLogAssetLoaderLogger();

        public void LogWarning(string message) => CustomLog.LogWarning(message);

        public void LogError(string message) => CustomLog.LogError(message);
    }
}
