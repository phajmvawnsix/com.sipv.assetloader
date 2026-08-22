namespace SiPV.AssetLoader
{
    // Default IAssetLoaderEventSink - discards everything.
    public sealed class NoOpAssetLoaderEventSink : IAssetLoaderEventSink
    {
        public static readonly NoOpAssetLoaderEventSink Instance = new NoOpAssetLoaderEventSink();

        public void Report(in AssetLoaderEvent loaderEvent)
        {
        }
    }
}
