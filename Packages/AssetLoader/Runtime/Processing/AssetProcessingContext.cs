namespace SiPV.AssetLoader
{
    // Context passed to IContentProcessor.ProcessAsync.
    public readonly struct AssetProcessingContext
    {
        public string Url { get; }

        public string ContentType { get; }

        // opaque caller payload (keys, auth tokens, etc) - pipeline doesn't care about its shape
        public object UserData { get; }

        public AssetProcessingContext(string url, string contentType, object userData)
        {
            Url = url;
            ContentType = contentType;
            UserData = userData;
        }
    }
}
