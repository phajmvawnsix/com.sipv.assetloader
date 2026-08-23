using System;
using System.Collections.Generic;
using UnityEngine;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Composition root: assembles every dependency an <see cref="IAssetLoader"/> needs, then
    /// produces an immutable <see cref="AssetLoaderConfig"/> via <see cref="Build"/>.
    /// </summary>
    /// <remarks>
    /// Two ways in. <see cref="CreateDefault"/> returns a builder with every slot already filled
    /// with the package's built-in implementations, which is what most projects want. Or construct
    /// the builder directly and set each slot yourself, in which case <see cref="Build"/> throws if
    /// you miss a mandatory one, so a misconfiguration fails at bootstrap rather than on a cache
    /// miss in front of a player. Either way you can override any individual slot before calling
    /// <see cref="Build"/>, since the setters are just last-write-wins.
    /// </remarks>
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

        /// <summary>
        /// Returns a builder pre-wired with every built-in implementation: HTTP source over
        /// UnityWebRequest, ref-counted RAM cache, file-backed disk cache plus its sidecar metadata
        /// store, hashed cache keys, the three default policies, and decoders for byte[], string,
        /// Texture2D, Sprite, AudioClip, and AssetBundle.
        /// </summary>
        /// <param name="cacheDirectoryPath">
        /// Where disk-cached bytes and their metadata live. Defaults to an "asset-cache" folder
        /// under <see cref="Application.persistentDataPath"/>, the only location writable on every
        /// platform Unity targets. Created if it doesn't exist.
        /// </param>
        /// <param name="diskBudgetBytes">
        /// Disk cache ceiling. Once exceeded, entries are evicted oldest-last-access-first after a
        /// write. Defaults to 256 MB.
        /// </param>
        /// <param name="ramMaxEntries">
        /// How many decoded assets stay resident. Only entries nobody holds a live handle to are
        /// eligible for eviction, so this is a target rather than a hard cap. Defaults to 64.
        /// </param>
        /// <param name="ramMaxBytes">
        /// RAM cache ceiling in estimated bytes, using <see cref="DefaultMemorySizeEstimator"/>.
        /// Same eviction caveat as <paramref name="ramMaxEntries"/>. Defaults to 64 MB.
        /// </param>
        /// <returns>
        /// A builder you can still customize (register extra decoders, add content processors,
        /// swap any default) before calling <see cref="Build"/>.
        /// </returns>
        public static AssetLoaderConfigBuilder CreateDefault(
            string cacheDirectoryPath = null,
            long diskBudgetBytes = 256L * 1024 * 1024,
            int ramMaxEntries = 64,
            long ramMaxBytes = 64L * 1024 * 1024)
        {
            var cacheDirectory = string.IsNullOrEmpty(cacheDirectoryPath)
                ? System.IO.Path.Combine(Application.persistentDataPath, "asset-cache")
                : cacheDirectoryPath;

            // One directory holds both stores. They stay separate objects (swappable independently)
            // but a shared path keeps a cache wipe to a single folder delete.
            var metadataStore = new FileDiskCacheMetadataStore(cacheDirectory);
            var diskCache = new FileDiskCache(cacheDirectory, metadataStore, diskBudgetBytes);

            var textureDecoder = new Texture2DDecoder();

            return new AssetLoaderConfigBuilder()
                .UseSource(new HttpAssetSource(new UnityWebRequestHttpClient()))
                .UseRamCache(new RamCache(
                    new DefaultMemorySizeEstimator(),
                    new DefaultAssetReleaser(),
                    ramMaxEntries,
                    ramMaxBytes))
                .UseDiskCache(diskCache, metadataStore)
                .UseCacheKeyProvider(new DefaultCacheKeyProvider())
                .UseTimeoutPolicy(new DefaultTimeoutPolicy())
                .UseRetryPolicy(new DefaultRetryPolicy())
                .UseCachePolicy(new DefaultCachePolicy())
                .RegisterDecoder(new ByteArrayDecoder())
                .RegisterDecoder(new StringDecoder())
                .RegisterDecoder(textureDecoder)
                // Shares the texture decoder instance rather than allocating a second one: a Sprite
                // is just a Texture2D decode plus a Sprite.Create wrap.
                .RegisterDecoder(new SpriteDecoder(textureDecoder))
                .RegisterDecoder(new AudioClipDecoder())
                .RegisterDecoder(new AssetBundleDecoder());
        }

        /// <summary>Sets where bytes come from on a cache miss. Mandatory.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
        public AssetLoaderConfigBuilder UseSource(IAssetSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            return this;
        }

        /// <summary>Sets the in-memory cache tier holding decoded, ref-counted assets. Mandatory.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="ramCache"/> is null.</exception>
        public AssetLoaderConfigBuilder UseRamCache(IRamCache ramCache)
        {
            _ramCache = ramCache ?? throw new ArgumentNullException(nameof(ramCache));
            return this;
        }

        /// <summary>
        /// Sets the persistent cache tier. Content and metadata are separate stores so either can be
        /// swapped independently, but they must be set together: every write or eviction touches
        /// both, and setting only one would let them drift apart. Mandatory.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        public AssetLoaderConfigBuilder UseDiskCache(IDiskCache diskCache, IDiskCacheMetadataStore metadataStore)
        {
            _diskCache = diskCache ?? throw new ArgumentNullException(nameof(diskCache));
            _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
            return this;
        }

        /// <summary>
        /// Sets how a request becomes a RAM key and a disk key. Override to add cache-busting, for
        /// example folding an app or content version into the key. Mandatory.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
        public AssetLoaderConfigBuilder UseCacheKeyProvider(ICacheKeyProvider provider)
        {
            _cacheKeyProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            return this;
        }

        /// <summary>
        /// Registers a decoder for <typeparamref name="T"/>. Call once per asset type you intend to
        /// load. Registering a second decoder for a type that already has one is allowed and the
        /// later registration wins, which is how you override a built-in decoder without touching
        /// package source.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="decoder"/> is null.</exception>
        public AssetLoaderConfigBuilder RegisterDecoder<T>(IAssetDecoder<T> decoder)
        {
            _decoderRegistry.Register(decoder);
            return this;
        }

        /// <summary>
        /// Appends a bytes-in/bytes-out transform (decrypt, decompress) that runs between fetch and
        /// decode. Optional: an empty chain passes bytes through untouched. Processors run in
        /// registration order, so register them in the order the bytes need to be unwrapped.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="processor"/> is null.</exception>
        public AssetLoaderConfigBuilder AddContentProcessor(IContentProcessor processor)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            _processors.Add(processor);
            return this;
        }

        /// <summary>
        /// Sets the global timeout policy. Individual requests can still override it via
        /// <see cref="AssetRequest.TimeoutPolicyOverride"/>. Mandatory.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="policy"/> is null.</exception>
        public AssetLoaderConfigBuilder UseTimeoutPolicy(ITimeoutPolicy policy)
        {
            _defaultTimeoutPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            return this;
        }

        /// <summary>
        /// Sets the global retry policy. Individual requests can still override it via
        /// <see cref="AssetRequest.RetryPolicyOverride"/>. Mandatory.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="policy"/> is null.</exception>
        public AssetLoaderConfigBuilder UseRetryPolicy(IRetryPolicy policy)
        {
            _defaultRetryPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            return this;
        }

        /// <summary>
        /// Sets the global freshness policy deciding hit/revalidate/miss against cached metadata.
        /// Individual requests can still override it via
        /// <see cref="AssetRequest.CachePolicyOverride"/>. Mandatory.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="policy"/> is null.</exception>
        public AssetLoaderConfigBuilder UseCachePolicy(ICachePolicy policy)
        {
            _defaultCachePolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            return this;
        }

        /// <summary>
        /// Redirects diagnostic warnings and errors. Optional: defaults to
        /// <see cref="CustomLogAssetLoaderLogger"/>. Pass <see cref="NoOpAssetLoaderLogger.Instance"/>
        /// to silence the package entirely.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
        public AssetLoaderConfigBuilder UseLogger(IAssetLoaderLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            return this;
        }

        /// <summary>
        /// Receives per-load telemetry (cache hit tier, dedup coalescing, retries, failures).
        /// Optional: defaults to <see cref="SipvLibEventAssetLoaderEventSink"/>, which republishes
        /// events on the SiPVLib event bus. Pass <see cref="NoOpAssetLoaderEventSink.Instance"/> to
        /// opt out, or your own implementation to drive a debug HUD or analytics.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sink"/> is null.</exception>
        public AssetLoaderConfigBuilder UseEventSink(IAssetLoaderEventSink sink)
        {
            _eventSink = sink ?? throw new ArgumentNullException(nameof(sink));
            return this;
        }

        /// <summary>
        /// Freezes the current setup into an immutable config, ready to hand to
        /// <see cref="AssetLoaderService"/>.
        /// </summary>
        /// <returns>An immutable snapshot. Later builder mutations do not affect it.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a mandatory dependency was never set. The message names the setter you
        /// missed.
        /// </exception>
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
