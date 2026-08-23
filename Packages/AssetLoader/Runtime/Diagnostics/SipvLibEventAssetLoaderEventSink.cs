using SiPVLib.Event;

namespace SiPV.AssetLoader
{
    /// <summary>Default <see cref="IAssetLoaderEventSink"/>: republishes events onto SiPVLib's pub/sub bus.</summary>
    /// <remarks>
    /// Routes through <c>EventManager.Invoke</c> (from <c>com.sipvlib.event</c>), so a HUD or
    /// analytics listener can subscribe via <c>EventManager.Add&lt;AssetLoaderEvent&gt;</c> without
    /// depending on <see cref="IAssetLoader"/> directly.
    /// </remarks>
    public sealed class SipvLibEventAssetLoaderEventSink : IAssetLoaderEventSink
    {
        /// <summary>Shared singleton instance.</summary>
        public static readonly SipvLibEventAssetLoaderEventSink Instance = new SipvLibEventAssetLoaderEventSink();

        /// <inheritdoc />
        public void Report(in AssetLoaderEvent loaderEvent) => EventManager.Invoke(loaderEvent);
    }
}
