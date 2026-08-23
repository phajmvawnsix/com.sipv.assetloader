namespace SiPV.AssetLoader
{
    /// <summary>
    /// Outcome of <see cref="IDiskCache.TryReadAsync"/>. Content only: freshness metadata comes
    /// from <see cref="IDiskCacheMetadataStore"/>.
    /// </summary>
    public readonly struct DiskCacheReadResult
    {
        /// <summary>True when bytes were found. Always check before reading <see cref="Content"/>.</summary>
        public bool Found { get; }

        /// <summary>
        /// The raw bytes exactly as written, before the content processor chain runs. Null when
        /// <see cref="Found"/> is false.
        /// </summary>
        public byte[] Content { get; }

        /// <summary>Creates a read result.</summary>
        /// <param name="found">Whether bytes were found.</param>
        /// <param name="content">The bytes, or null on a miss.</param>
        public DiskCacheReadResult(bool found, byte[] content)
        {
            Found = found;
            Content = content;
        }

        /// <summary>A reusable "nothing cached" result.</summary>
        public static DiskCacheReadResult Miss { get; } = new DiskCacheReadResult(false, null);
    }
}
