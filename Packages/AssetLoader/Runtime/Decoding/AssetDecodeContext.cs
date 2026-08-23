namespace SiPV.AssetLoader
{
    /// <summary>Everything a decoder knows about the asset besides its bytes.</summary>
    public readonly struct AssetDecodeContext
    {
        /// <summary>Source URL. Useful for error messages, and for decoders that key off path structure.</summary>
        public string Url { get; }

        /// <summary>
        /// Content type the source reported, or null when it reported none. Generally the more
        /// reliable signal of the two, since a URL extension can be absent or wrong.
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// Extension derived from <see cref="Url"/>: lowercase, no leading dot, query string
        /// ignored. Empty when the URL has none.
        /// </summary>
        public string Extension { get; }

        /// <summary>
        /// The request's <see cref="AssetRequest.UserData"/>, passed through untouched. Use it to
        /// hand decode parameters to your own decoder; the pipeline never inspects it.
        /// </summary>
        public object UserData { get; }

        /// <summary>Creates a decode context. Built by the pipeline; decoders only read it.</summary>
        /// <param name="url">Source URL.</param>
        /// <param name="contentType">Reported content type, or null.</param>
        /// <param name="extension">Normalised extension, or empty.</param>
        /// <param name="userData">Passthrough payload from the request.</param>
        public AssetDecodeContext(string url, string contentType, string extension, object userData)
        {
            Url = url;
            ContentType = contentType;
            Extension = extension;
            UserData = userData;
        }
    }
}
