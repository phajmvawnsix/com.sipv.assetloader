namespace SiPV.AssetLoader
{
    /// <summary>Diagnostic logging hook, decoupled from UnityEngine.Debug.Log so it can be redirected or silenced.</summary>
    public interface IAssetLoaderLogger
    {
        // Something's off but the pipeline can keep going (e.g. a retry).
        void LogWarning(string message);

        // The pipeline gave up on something - worth surfacing in the console.
        void LogError(string message);
    }
}
