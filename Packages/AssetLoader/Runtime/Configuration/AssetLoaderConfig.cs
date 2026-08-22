namespace SiPV.AssetLoader
{
    // Immutable resolved config for an AssetLoader, built only via AssetLoaderConfigBuilder.Build.
    public sealed class AssetLoaderConfig
    {
        public IAssetSource Source { get; }

        public IRamCache RamCache { get; }

        public IDiskCache DiskCache { get; }

        public IDiskCacheMetadataStore MetadataStore { get; }

        public ICacheKeyProvider CacheKeyProvider { get; }

        public IAssetDecoderRegistry DecoderRegistry { get; }

        public IContentProcessorPipeline ProcessorPipeline { get; }

        public ITimeoutPolicy DefaultTimeoutPolicy { get; }

        public IRetryPolicy DefaultRetryPolicy { get; }

        public ICachePolicy DefaultCachePolicy { get; }

        public IAssetLoaderLogger Logger { get; }

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
