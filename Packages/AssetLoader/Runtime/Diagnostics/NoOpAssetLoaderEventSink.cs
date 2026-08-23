namespace SiPV.AssetLoader
{
    /// <summary>Opt-out <see cref="IAssetLoaderEventSink"/> that discards every event.</summary>
    /// <remarks>
    /// Use this to disable diagnostics reporting entirely; the default sink is
    /// <see cref="SipvLibEventAssetLoaderEventSink"/>, not this one.
    /// </remarks>
    public sealed class NoOpAssetLoaderEventSink : IAssetLoaderEventSink
    {
        /// <summary>Shared singleton instance.</summary>
        public static readonly NoOpAssetLoaderEventSink Instance = new NoOpAssetLoaderEventSink();

        /// <inheritdoc />
        public void Report(in AssetLoaderEvent loaderEvent)
        {
        }
    }
}
