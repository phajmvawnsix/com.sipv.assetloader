using SiPVLib.Debugging;

namespace SiPV.AssetLoader
{
    /// <summary>Default <see cref="IAssetLoaderLogger"/>: routes through SiPVLib's <c>CustomLog</c>.</summary>
    /// <remarks>
    /// <c>CustomLog</c> (from <c>com.sipvlib.debugging</c>) wraps <c>UnityEngine.Debug</c> and can
    /// be silenced globally via <c>LOGGING_DISABLE</c>. Use <see cref="NoOpAssetLoaderLogger"/>
    /// instead to silence just this package without affecting other <c>CustomLog</c> callers.
    /// </remarks>
    public sealed class CustomLogAssetLoaderLogger : IAssetLoaderLogger
    {
        /// <summary>Shared singleton instance.</summary>
        public static readonly CustomLogAssetLoaderLogger Instance = new CustomLogAssetLoaderLogger();

        /// <inheritdoc />
        public void LogWarning(string message) => CustomLog.LogWarning(message);

        /// <inheritdoc />
        public void LogError(string message) => CustomLog.LogError(message);
    }
}
