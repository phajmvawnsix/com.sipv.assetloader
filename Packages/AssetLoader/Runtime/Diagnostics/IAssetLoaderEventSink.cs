namespace SiPV.AssetLoader
{
    /// <summary>Telemetry hook for pipeline events (cache hits/misses, dedup, retries). Default is NoOpAssetLoaderEventSink.</summary>
    public interface IAssetLoaderEventSink
    {
        // Called inline on the loader's calling thread - keep implementations cheap and non-blocking.
        void Report(in AssetLoaderEvent loaderEvent);
    }
}
