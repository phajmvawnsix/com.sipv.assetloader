using SiPVLib.Event;

namespace SiPV.AssetLoader
{
    // Republishes AssetLoaderEvent onto SiPVLib's pub/sub bus (EventManager.Invoke), so a HUD/analytics
    // can subscribe via EventManager.Add<AssetLoaderEvent> without depending on IAssetLoader directly.
    public sealed class SipvLibEventAssetLoaderEventSink : IAssetLoaderEventSink
    {
        public static readonly SipvLibEventAssetLoaderEventSink Instance = new SipvLibEventAssetLoaderEventSink();

        public void Report(in AssetLoaderEvent loaderEvent) => EventManager.Invoke(loaderEvent);
    }
}
