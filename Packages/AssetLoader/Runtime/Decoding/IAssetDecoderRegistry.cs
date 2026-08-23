namespace SiPV.AssetLoader
{
    /// <summary>
    /// Holds the registered decoders and resolves one for a requested type. Populated through
    /// <see cref="AssetLoaderConfigBuilder.RegisterDecoder{T}"/>.
    /// </summary>
    /// <remarks>
    /// Registration happens once at bootstrap, resolution happens on every cache-missing load, and
    /// both are main-thread only. Implementations therefore need no locking.
    /// </remarks>
    public interface IAssetDecoderRegistry
    {
        /// <summary>Adds a decoder for <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">Asset type the decoder produces.</typeparam>
        /// <param name="decoder">The decoder to register.</param>
        /// <remarks>
        /// Registering a second decoder for a type that already has one is allowed, and the later
        /// registration is matched first. That is what lets a consumer override a built-in decoder
        /// without touching package source.
        /// </remarks>
        void Register<T>(IAssetDecoder<T> decoder);

        /// <summary>Finds a decoder able to produce <typeparamref name="T"/> from this content.</summary>
        /// <typeparam name="T">Requested asset type.</typeparam>
        /// <param name="contentType">Content type reported by the source, may be null.</param>
        /// <param name="extension">Extension derived from the URL, lowercase and without a dot, may be empty.</param>
        /// <param name="decoder">The matched decoder, or null when nothing matched.</param>
        /// <returns>True when a decoder matched.</returns>
        /// <remarks>
        /// Candidates are filtered by type first, then asked whether they handle the content type
        /// or the extension. A false return surfaces to the caller as
        /// <see cref="AssetLoadErrorCode.DecodeFailed"/>.
        /// </remarks>
        bool TryResolve<T>(string contentType, string extension, out IAssetDecoder<T> decoder);
    }
}
