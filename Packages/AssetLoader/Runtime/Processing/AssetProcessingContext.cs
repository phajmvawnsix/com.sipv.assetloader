namespace SiPV.AssetLoader
{
    /// <summary>Context passed to <see cref="IContentProcessor.ProcessAsync"/>.</summary>
    public readonly struct AssetProcessingContext
    {
        /// <summary>The URL being loaded.</summary>
        public string Url { get; }

        /// <summary>The content type reported by the source, if any.</summary>
        public string ContentType { get; }

        /// <summary>
        /// Opaque caller payload, such as decryption keys or auth tokens. The pipeline does not
        /// interpret its shape; it is forwarded from <see cref="AssetRequest"/> as-is.
        /// </summary>
        public object UserData { get; }

        /// <summary>Creates a processing context.</summary>
        public AssetProcessingContext(string url, string contentType, object userData)
        {
            Url = url;
            ContentType = contentType;
            UserData = userData;
        }
    }
}
