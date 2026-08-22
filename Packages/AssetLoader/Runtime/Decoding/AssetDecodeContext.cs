namespace SiPV.AssetLoader
{
    // Passed to IAssetDecoder.DecodeAsync alongside the bytes to decode.
    public readonly struct AssetDecodeContext
    {
        public string Url { get; }

        public string ContentType { get; }

        // Derived from Url, no leading dot, lowercase.
        public string Extension { get; }

        // Passthrough of AssetRequest.UserData for custom decoders that need extra params.
        public object UserData { get; }

        public AssetDecodeContext(string url, string contentType, string extension, object userData)
        {
            Url = url;
            ContentType = contentType;
            Extension = extension;
            UserData = userData;
        }
    }
}
