namespace SiPV.AssetLoader
{
    // Registry mapping (type, content-type/extension) to a decoder instance, populated by AssetLoaderConfigBuilder.
    public interface IAssetDecoderRegistry
    {
        // Later registration wins on conflict, so consumers can override a built-in decoder.
        void Register<T>(IAssetDecoder<T> decoder);

        bool TryResolve<T>(string contentType, string extension, out IAssetDecoder<T> decoder);
    }
}
