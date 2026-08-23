namespace SiPV.AssetLoader
{
    /// <summary>
    /// An immutable, fully resolved set of dependencies for one <see cref="AssetLoaderService"/>.
    /// Produced only by <see cref="AssetLoaderConfigBuilder.Build"/>.
    /// </summary>
    /// <remarks>
    /// Every dependency is exposed so a host application can reach past the loader when it needs
    /// to, for example to report cache statistics or wipe the disk cache. The loader itself never
    /// mutates this object.
    /// </remarks>
    public sealed class AssetLoaderConfig
    {
        /// <summary>Where bytes come from on a cache miss.</summary>
        public IAssetSource Source { get; }

        /// <summary>In-memory tier of decoded, ref-counted assets. Synchronous and main-thread only.</summary>
        public IRamCache RamCache { get; }

        /// <summary>
        /// Persistent tier holding raw pre-processing bytes. Always evict in step with
        /// <see cref="MetadataStore"/> or the two drift apart.
        /// </summary>
        public IDiskCache DiskCache { get; }

        /// <summary>Freshness metadata (ETag, max-age, timestamps) for the disk tier.</summary>
        public IDiskCacheMetadataStore MetadataStore { get; }

        /// <summary>Turns a request into its RAM key and disk key.</summary>
        public ICacheKeyProvider CacheKeyProvider { get; }

        /// <summary>Resolves the decoder for a requested asset type.</summary>
        public IAssetDecoderRegistry DecoderRegistry { get; }

        /// <summary>The bytes-to-bytes transform chain applied between fetch and decode.</summary>
        public IContentProcessorPipeline ProcessorPipeline { get; }

        /// <summary>Timeout policy used when a request does not override it.</summary>
        public ITimeoutPolicy DefaultTimeoutPolicy { get; }

        /// <summary>Retry policy used when a request does not override it.</summary>
        public IRetryPolicy DefaultRetryPolicy { get; }

        /// <summary>Cache freshness policy used when a request does not override it.</summary>
        public ICachePolicy DefaultCachePolicy { get; }

        /// <summary>Where the package sends diagnostic warnings and errors.</summary>
        public IAssetLoaderLogger Logger { get; }

        /// <summary>Where the package sends per-load telemetry.</summary>
        public IAssetLoaderEventSink EventSink { get; }

        internal AssetLoaderConfig(
            IAssetSource source,
            IRamCache ramCache,
            IDiskCache diskCache,
            IDiskCacheMetadataStore metadataStore,
            ICacheKeyProvider cacheKeyProvider,
            IAssetDecoderRegistry decoderRegistry,
            IContentProcessorPipeline processorPipeline,
            ITimeoutPolicy defaultTimeoutPolicy,
            IRetryPolicy defaultRetryPolicy,
            ICachePolicy defaultCachePolicy,
            IAssetLoaderLogger logger,
            IAssetLoaderEventSink eventSink)
        {
            Source = source;
            RamCache = ramCache;
            DiskCache = diskCache;
            MetadataStore = metadataStore;
            CacheKeyProvider = cacheKeyProvider;
            DecoderRegistry = decoderRegistry;
            ProcessorPipeline = processorPipeline;
            DefaultTimeoutPolicy = defaultTimeoutPolicy;
            DefaultRetryPolicy = defaultRetryPolicy;
            DefaultCachePolicy = defaultCachePolicy;
            Logger = logger;
            EventSink = eventSink;
        }
    }
}
