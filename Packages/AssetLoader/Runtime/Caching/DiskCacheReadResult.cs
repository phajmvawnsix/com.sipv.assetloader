namespace SiPV.AssetLoader
{
    // Result of IDiskCache.TryReadAsync. Content only, metadata lives in IDiskCacheMetadataStore.
    public readonly struct DiskCacheReadResult
    {
        public bool Found { get; }

        // raw bytes as written, pre content-processing; null when Found is false
        public byte[] Content { get; }

        public DiskCacheReadResult(bool found, byte[] content)
        {
            Found = found;
            Content = content;
        }

        public static DiskCacheReadResult Miss { get; } = new DiskCacheReadResult(false, null);
    }
}
