using System;
using System.Collections.Generic;

namespace SiPV.AssetLoader
{
    // Composition root for AssetLoaderConfig. Every mandatory slot has to be set explicitly or Build() throws.
    // TODO: add CreateDefault() once there's a default disk cache + HTTP source worth pointing it at.
    public sealed class AssetLoaderConfigBuilder
    {
        private IAssetSource _source;
        private IRamCache _ramCache;
        private IDiskCache _diskCache;
        private IDiskCacheMetadataStore _metadataStore;
        private ICacheKeyProvider _cacheKeyProvider;
        private readonly AssetDecoderRegistry _decoderRegistry = new AssetDecoderRegistry();
        private readonly List<IContentProcessor> _processors = new List<IContentProcessor>();
        private ITimeoutPolicy _defaultTimeoutPolicy;
        private IRetryPolicy _defaultRetryPolicy;
        private ICachePolicy _defaultCachePolicy;
        private IAssetLoaderLogger _logger;
        private IAssetLoaderEventSink _eventSink;

        public AssetLoaderConfigBuilder UseSource(IAssetSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            return this;
        }

        public AssetLoaderConfigBuilder UseRamCache(IRamCache ramCache)
        {
            _ramCache = ramCache ?? throw new ArgumentNullException(nameof(ramCache));
            return this;
        }

        // Pair, deliberately: content and metadata drift apart if you can set one without the other.
        public AssetLoaderConfigBuilder UseDiskCache(IDiskCache diskCache, IDiskCacheMetadataStore metadataStore)
        {
            _diskCache = diskCache ?? throw new ArgumentNullException(nameof(diskCache));
            _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
            return this;
        }

        public AssetLoaderConfigBuilder UseCacheKeyProvider(ICacheKeyProvider provider)
        {
            _cacheKeyProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            return this;
        }

        public AssetLoaderConfigBuilder RegisterDecoder<T>(IAssetDecoder<T> decoder)
        {
            _decoderRegistry.Register(decoder);
            return this;
        }

        // Appends a content processor (e.g. decrypt) to the chain, runs in registration order.
        public AssetLoaderConfigBuilder AddContentProcessor(IContentProcessor processor)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            _processors.Add(processor);
            return this;
        }

        public AssetLoaderConfigBuilder UseTimeoutPolicy(ITimeoutPolicy policy)
        {
            _defaultTimeoutPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            return this;
        }

        public AssetLoaderConfigBuilder UseRetryPolicy(IRetryPolicy policy)
        {
            _defaultRetryPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            return this;
        }

        public AssetLoaderConfigBuilder UseCachePolicy(ICachePolicy policy)
        {
            _defaultCachePolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            return this;
        }

        // Optional, falls back to CustomLogAssetLoaderLogger.
        public AssetLoaderConfigBuilder UseLogger(IAssetLoaderLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            return this;
        }

        // Optional, falls back to SipvLibEventAssetLoaderEventSink.
        public AssetLoaderConfigBuilder UseEventSink(IAssetLoaderEventSink sink)
        {
            _eventSink = sink ?? throw new ArgumentNullException(nameof(sink));
            return this;
        }

        // Throws InvalidOperationException if any mandatory slot was never set. Empty processor chain
        // just passes through.
        public AssetLoaderConfig Build()
        {
            RequireSet(_source, nameof(UseSource));
            RequireSet(_ramCache, nameof(UseRamCache));
            RequireSet(_diskCache, nameof(UseDiskCache));
            RequireSet(_metadataStore, nameof(UseDiskCache));
            RequireSet(_cacheKeyProvider, nameof(UseCacheKeyProvider));
            RequireSet(_defaultTimeoutPolicy, nameof(UseTimeoutPolicy));
            RequireSet(_defaultRetryPolicy, nameof(UseRetryPolicy));
            RequireSet(_defaultCachePolicy, nameof(UseCachePolicy));

            return new AssetLoaderConfig(
                _source,
                _ramCache,
                _diskCache,
                _metadataStore,
                _cacheKeyProvider,
                _decoderRegistry,
                new ContentProcessorPipeline(_processors),
                _defaultTimeoutPolicy,
                _defaultRetryPolicy,
                _defaultCachePolicy,
                _logger ?? CustomLogAssetLoaderLogger.Instance,
                _eventSink ?? SipvLibEventAssetLoaderEventSink.Instance);
        }

        private static void RequireSet(object value, string setterName)
        {
            if (value == null)
            {
                throw new InvalidOperationException(
                    $"AssetLoaderConfigBuilder.Build() requires {setterName}(...) to be called first.");
            }
        }
    }
}
