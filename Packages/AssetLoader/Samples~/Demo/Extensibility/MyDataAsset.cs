namespace SiPV.AssetLoader.Samples.Demo
{
    // Stand-in for a real custom asset type (fbx, usd, a proprietary level format, etc).
    // The pattern shown here doesn't depend on what this class actually contains.
    public sealed class MyDataAsset
    {
        public string Payload { get; }

        public MyDataAsset(string payload)
        {
            Payload = payload;
        }
    }
}
