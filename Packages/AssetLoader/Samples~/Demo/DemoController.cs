using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using SiPV.AssetLoader;

namespace SiPV.AssetLoader.Samples.Demo
{
    // Single-file, OnGUI-driven demo. Deliberately not a Canvas/Button scene: the point of this
    // sample is proving the package's own behavior (cache tiers, dedup, custom asset input), not
    // showing off UI work, and OnGUI needs zero Inspector-wired references - every dependency
    // is constructed right here in code, so the scene file is just this one component on one
    // GameObject. See README.md for what each button proves.
    public sealed class DemoController : MonoBehaviour
    {
        // Public URL, needs no local server at all - every flow below works with this out of the
        // box. Fixed id = same image every time, deterministic.
        [SerializeField] private string _imageUrl = "https://picsum.photos/id/237/512";

        private AssetLoaderService _loader;
        private AssetLoaderConfig _config;
        private CountingHttpClient _countingHttpClient;
        private DefaultCacheKeyProvider _keyProvider;

        private AssetHandle<Texture2D> _currentHandle; // class, null when nothing's loaded yet
        private Texture2D _currentTexture;
        private string _status = "Not loaded yet.";

        // IAssetLoaderEventSink.Report runs inline on whatever thread the pipeline happens to be
        // on when it fires - LoadAndCacheAsync spends most of its body off the main thread
        // (UniTask.SwitchToThreadPool), so most events actually arrive from a thread pool thread,
        // not the main thread OnGUI runs on. List<T> isn't thread-safe, so every access needs
        // the lock below - without it this races against OnGUI's own read every frame, worse the
        // more concurrent loads are in flight at once (5 concurrent = 5 threads racing to Add()).
        private readonly object _eventLogLock = new object();
        private readonly List<string> _eventLog = new List<string>();
        private Vector2 _eventScroll;

        // key -> approximate byte size, tracked ourselves since IRamCache has no stats accessor
        // (a deliberate omission - see docs/DesignDraft.txt - the cache doesn't need to answer
        // "what's in you" for the pipeline to work, only this demo wants that view)
        private readonly Dictionary<string, long> _ramTrackedEntries = new Dictionary<string, long>();

        private bool _busy;

        // "Load any asset at runtime" section - separate from the fixed-URL flows above,
        // demonstrates the same LoadAsync<T> call against whatever the user types in,
        // local device path or web URL, with the target type picked at runtime too.
        // MyData is Extensibility/MyDataDecoder.cs, a custom decoder registered the same way
        // a consumer's own would be - see the Extensibility/ folder for the other 2 samples.
        private enum AssetKind { Texture2D, AudioClip, Text, MyData }

        private string _customPathInput = "";
        private int _customKindIndex;
        private string _customStatus = "";

        private AssetHandle<Texture2D> _customTextureHandle;
        private AssetHandle<AudioClip> _customAudioHandle;
        private AssetHandle<string> _customTextHandle;
        private AssetHandle<MyDataAsset> _customMyDataHandle;
        private AudioSource _audioSource;

        // Extensibility/XorContentProcessor.cs and Extensibility/SingleRetryPolicy.cs, proven
        // live against the real pipeline below rather than just described in a README.
        private XorContentProcessor _xorProcessor;
        private static readonly byte[] XorDemoKey = Encoding.UTF8.GetBytes("demo-key");
        private const string XorMarker = "xor-demo";
        private string _xorDemoStatus = "";

        private bool _useSingleRetryPolicy;
        private string _retryDemoStatus = "";
        private const string RetryDemoUrl = "https://httpstat.us/500";

        private void Awake()
        {
            _keyProvider = new DefaultCacheKeyProvider();
            _countingHttpClient = new CountingHttpClient(new UnityWebRequestHttpClient());

            var diskCacheDir = System.IO.Path.Combine(Application.persistentDataPath, "assetloader-demo-cache");
            var metadataStore = new FileDiskCacheMetadataStore(diskCacheDir);
            var diskCache = new FileDiskCache(diskCacheDir, metadataStore, budgetBytes: 64L * 1024 * 1024);

            _xorProcessor = new XorContentProcessor(XorDemoKey);

            _config = new AssetLoaderConfigBuilder()
                .UseSource(new HttpAssetSource(_countingHttpClient))
                .UseRamCache(new RamCache(new DefaultMemorySizeEstimator(), new DefaultAssetReleaser(), maxEntries: 32, maxBytes: 32L * 1024 * 1024))
                .UseDiskCache(diskCache, metadataStore)
                .UseCacheKeyProvider(_keyProvider)
                .UseTimeoutPolicy(new DefaultTimeoutPolicy(perAttemptTimeout: TimeSpan.FromSeconds(10)))
                .UseRetryPolicy(new DefaultRetryPolicy())
                .UseCachePolicy(new DefaultCachePolicy())
                .RegisterDecoder(new Texture2DDecoder())
                .RegisterDecoder(new AudioClipDecoder())
                .RegisterDecoder(new StringDecoder())
                .RegisterDecoder(new MyDataDecoder())
                // The real XorContentProcessor runs unconditionally over EVERY load if just
                // registered directly - every other demo asset (picsum image, plain text, etc)
                // isn't actually XOR-encoded, so that would corrupt them. This wrapper only
                // applies it to requests carrying the xor-demo marker, everything else passes
                // through untouched - a demo-only conditional, not something the package needs
                // (a real project either XOR's everything behind one source, or doesn't).
                .AddContentProcessor(new ConditionalProcessor(_xorProcessor, XorMarker))
                .UseEventSink(new DemoEventSink(this))
                .Build();

            _loader = new AssetLoaderService(_config);
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void OnDestroy()
        {
            _currentHandle?.Release();
            _customTextureHandle?.Release();
            _customAudioHandle?.Release();
            _customTextHandle?.Release();
            _customMyDataHandle?.Release();
        }

        // Called by DemoEventSink on every pipeline event (RAM hit, disk hit, dedup, etc) - this
        // is exactly what the demo needs to show "where did this load actually come from",
        // no separate diagnostics interface required (IAssetLoaderEventSink already covers it).
        internal void OnPipelineEvent(AssetLoaderEvent loaderEvent)
        {
            var line = loaderEvent.Duration.HasValue
                ? $"[{DateTime.Now:HH:mm:ss.fff}] {loaderEvent.Kind} ({loaderEvent.Key}) - {loaderEvent.Duration.Value.TotalMilliseconds:F0}ms"
                : $"[{DateTime.Now:HH:mm:ss.fff}] {loaderEvent.Kind} ({loaderEvent.Key})";

            lock (_eventLogLock)
            {
                _eventLog.Add(line);
                if (_eventLog.Count > 200)
                {
                    _eventLog.RemoveAt(0);
                }
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 460, Screen.height - 20), GUI.skin.box);

            GUILayout.Label("AssetLoader Demo", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold });
            GUILayout.Space(4);
            GUILayout.Label($"URL: {_imageUrl}");
            GUILayout.Space(8);

            GUI.enabled = !_busy;

            if (GUILayout.Button("Load (miss first time, RAM hit after)"))
            {
                LoadOnceAsync(_imageUrl).Forget();
            }

            if (GUILayout.Button("Clear RAM cache"))
            {
                ClearRam();
            }

            if (GUILayout.Button("Clear disk cache"))
            {
                ClearDiskAsync().Forget();
            }

            if (GUILayout.Button("Load 5 concurrent (dedup proof)"))
            {
                LoadFiveConcurrentAsync().Forget();
            }

            GUI.enabled = true;

            GUILayout.Space(8);
            GUILayout.Label(_status);

            if (_currentTexture != null)
            {
                var rect = GUILayoutUtility.GetRect(200, 200);
                GUI.DrawTexture(rect, _currentTexture, ScaleMode.ScaleToFit);
            }

            GUILayout.Space(8);
            DrawCustomLoadSection();

            GUILayout.Space(8);
            DrawExtensibilitySamplesSection();

            GUILayout.Space(8);
            DrawCacheStats();

            GUILayout.Space(8);
            GUILayout.Label($"HTTP requests made this session: {_countingHttpClient.RequestCount}");

            GUILayout.Space(8);
            GUILayout.Label("Event log:");
            string[] eventLogSnapshot;
            lock (_eventLogLock)
            {
                eventLogSnapshot = _eventLog.ToArray();
            }
            _eventScroll = GUILayout.BeginScrollView(_eventScroll, GUI.skin.box, GUILayout.Height(180));
            for (var i = eventLogSnapshot.Length - 1; i >= 0; i--)
            {
                GUILayout.Label(eventLogSnapshot[i]);
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawCacheStats()
        {
            long ramBytes = 0;
            foreach (var bytes in _ramTrackedEntries.Values)
            {
                ramBytes += bytes;
            }

            GUILayout.Label($"RAM cache (tracked by demo): {_ramTrackedEntries.Count} entries, ~{ramBytes / 1024} KB");

            // Disk stats come straight from the real cache, no demo-side bookkeeping needed -
            // IDiskCache/IDiskCacheMetadataStore are exposed on AssetLoaderConfig for exactly this.
            GUILayout.Label(_diskStatsLine);
        }

        private void DrawCustomLoadSection()
        {
            GUILayout.Label("Load any asset at runtime:");

            GUILayout.BeginHorizontal();
            _customPathInput = GUILayout.TextField(_customPathInput);
#if UNITY_EDITOR
            if (GUILayout.Button("Browse...", GUILayout.Width(80)))
            {
                BrowseForFile();
            }
#endif
            GUILayout.EndHorizontal();

#if UNITY_EDITOR
            GUILayout.Label("(or type a web URL like https://... - Browse only works in-Editor, native file pickers on device builds need a per-platform plugin, out of scope for this sample)");
#else
            GUILayout.Label("(web URL like https://..., or a local device path)");
#endif

            _customKindIndex = GUILayout.Toolbar(_customKindIndex, new[] { "Texture2D", "AudioClip", "Text", "MyData" });

            if ((AssetKind)_customKindIndex == AssetKind.MyData && GUILayout.Button("Create demo .mydata file (Extensibility/MyDataDecoder.cs)"))
            {
                CreateMyDataDemoFile();
            }

            GUI.enabled = !_busy && !string.IsNullOrWhiteSpace(_customPathInput);
            if (GUILayout.Button("Load"))
            {
                LoadCustomAsync((AssetKind)_customKindIndex, _customPathInput).Forget();
            }
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_customStatus))
            {
                GUILayout.Label(_customStatus);
            }

            if (_customTextureHandle != null)
            {
                var rect = GUILayoutUtility.GetRect(200, 200);
                GUI.DrawTexture(rect, _customTextureHandle.Asset, ScaleMode.ScaleToFit);
            }

            if (_customAudioHandle != null && GUILayout.Button(_audioSource.isPlaying ? "Stop" : "Play"))
            {
                if (_audioSource.isPlaying)
                {
                    _audioSource.Stop();
                }
                else
                {
                    _audioSource.PlayOneShot(_customAudioHandle.Asset);
                }
            }

            if (_customTextHandle != null)
            {
                GUILayout.TextArea(_customTextHandle.Asset, GUILayout.Height(80));
            }

            if (_customMyDataHandle != null)
            {
                GUILayout.Label($"MyData payload (Extensibility/MyDataDecoder.cs's decoder, resolved by \"mydata\" extension): {_customMyDataHandle.Asset.Payload}");
            }
        }

        // Extensibility/XorContentProcessor.cs and Extensibility/SingleRetryPolicy.cs, proven
        // live against the real pipeline rather than just described. MyDataDecoder's proof
        // lives in the "Load any asset" section above (the MyData type option) since it fits
        // that flow directly; these two get their own controls since they don't.
        private void DrawExtensibilitySamplesSection()
        {
            GUILayout.Label("Extensibility samples:");

            GUI.enabled = !_busy;
            if (GUILayout.Button("Load XOR-encrypted demo text (Extensibility/XorContentProcessor.cs)"))
            {
                LoadXorDemoAsync().Forget();
            }
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(_xorDemoStatus))
            {
                GUILayout.Label(_xorDemoStatus);
            }

            GUILayout.Space(4);
            _useSingleRetryPolicy = GUILayout.Toggle(_useSingleRetryPolicy, "Use SingleRetryPolicy (Extensibility/SingleRetryPolicy.cs - retry once, no backoff)");
            GUI.enabled = !_busy;
            if (GUILayout.Button("Load a guaranteed-500 URL (compare retry behavior)"))
            {
                LoadRetryPolicyDemoAsync().Forget();
            }
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(_retryDemoStatus))
            {
                GUILayout.Label(_retryDemoStatus);
            }
        }

        // Symmetric XOR: running the real XorContentProcessor once on plain bytes IS the
        // "encrypt" step (see the sample's own comment on why this class doubles as both
        // directions) - no separate encrypt code needed, just call it before writing the file.
        private async UniTaskVoid LoadXorDemoAsync()
        {
            _busy = true;
            _xorDemoStatus = "Encrypting + loading...";

            try
            {
                const string original = "Hello from an XOR round-trip through the real pipeline!";
                var plainBytes = Encoding.UTF8.GetBytes(original);
                var context = new AssetProcessingContext("demo://xor", "text/plain", null);
                var encryptedBytes = await _xorProcessor.ProcessAsync(plainBytes, context, CancellationToken.None);

                var path = System.IO.Path.Combine(Application.temporaryCachePath, $"{XorMarker}_{Guid.NewGuid():N}.txt");
                System.IO.File.WriteAllBytes(path, encryptedBytes);

                // ConditionalProcessor (registered in Awake) only applies XorContentProcessor to
                // requests whose URL contains "xor-demo" - this file's name carries that marker,
                // so the pipeline decrypts it automatically on the way through, same as it would
                // for any other content processor.
                using var handle = await _loader.LoadAsync<string>(new AssetRequest(new Uri(path).AbsoluteUri));

                _xorDemoStatus = handle.Asset == original
                    ? $"Round-trip OK: decrypted text matches original (\"{handle.Asset}\")."
                    : $"Round-trip MISMATCH - decrypted to: \"{handle.Asset}\"";
            }
            catch (Exception ex)
            {
                _xorDemoStatus = $"XOR demo failed: {ex.Message}";
            }
            finally
            {
                _busy = false;
            }
        }

        private async UniTaskVoid LoadRetryPolicyDemoAsync()
        {
            _busy = true;
            var policyName = _useSingleRetryPolicy ? "SingleRetryPolicy (1 retry, no backoff)" : "DefaultRetryPolicy (3 attempts, exponential backoff)";
            _retryDemoStatus = $"Loading via {policyName}...";

            try
            {
                var request = new AssetRequest(
                    RetryDemoUrl,
                    retryPolicyOverride: _useSingleRetryPolicy ? new SingleRetryPolicy() : null);

                var stopwatch = Stopwatch.StartNew();
                using var handle = await _loader.LoadAsync<string>(request);
                stopwatch.Stop();
                _retryDemoStatus = $"Unexpectedly succeeded in {stopwatch.ElapsedMilliseconds}ms via {policyName}.";
            }
            catch (Exception ex)
            {
                _retryDemoStatus = $"Failed as expected via {policyName} - see event log for RetryAttempted count ({ex.GetType().Name}: {ex.Message}).";
            }
            finally
            {
                _busy = false;
            }
        }

        // Demo-only conditional wrapper - see the AddContentProcessor call in Awake() for why
        // this exists instead of registering XorContentProcessor directly.
        private sealed class ConditionalProcessor : IContentProcessor
        {
            private readonly IContentProcessor _inner;
            private readonly string _urlMarker;

            public ConditionalProcessor(IContentProcessor inner, string urlMarker)
            {
                _inner = inner;
                _urlMarker = urlMarker;
            }

            public UniTask<byte[]> ProcessAsync(byte[] input, AssetProcessingContext context, CancellationToken cancellationToken) =>
                context.Url != null && context.Url.Contains(_urlMarker)
                    ? _inner.ProcessAsync(input, context, cancellationToken)
                    : UniTask.FromResult(input);
        }

#if UNITY_EDITOR
        // Editor-only native file dialog (UnityEditor.EditorUtility, built into Unity - no
        // plugin or external library needed). Device builds (Standalone/Android/iOS) have no
        // Unity-provided cross-platform equivalent - a real native picker there needs a
        // per-platform plugin (Win32 comdlg32 P/Invoke, a Java Intent on Android, a
        // UIDocumentPickerViewController on iOS, ...), confirmed with user as out of scope for
        // this sample. The text field still works fine there, this button is purely a
        // convenience for testing in the Editor.
        private void BrowseForFile()
        {
            var filters = (AssetKind)_customKindIndex switch
            {
                AssetKind.Texture2D => new[] { "Image files", "png,jpg,jpeg", "All files", "*" },
                AssetKind.AudioClip => new[] { "Audio files", "wav,ogg,mp3", "All files", "*" },
                AssetKind.MyData => new[] { "MyData files", "mydata", "All files", "*" },
                _ => new[] { "Text files", "txt,json,xml,csv", "All files", "*" },
            };

            var path = UnityEditor.EditorUtility.OpenFilePanelWithFilters("Select asset to load", "", filters);
            if (!string.IsNullOrEmpty(path))
            {
                _customPathInput = path;
            }
        }
#endif

        // A bare path (no scheme) is treated as a local device path and converted to a
        // file:// URI - UnityWebRequest (what HttpAssetSource runs on) reads file:// URIs
        // directly, so this reuses the exact same source/cache/decode pipeline as a real
        // web URL, no separate local-file code path needed anywhere in the package.
        private static string ResolveUrl(string input)
        {
            var trimmed = input.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return new Uri(System.IO.Path.GetFullPath(trimmed)).AbsoluteUri;
        }

        // Writes a throwaway ".mydata" file and points the input field at it - MyDataDecoder
        // (Extensibility/MyDataDecoder.cs) resolves purely by the ".mydata" extension, so this is
        // all a consumer needs to try the custom-decoder flow without hand-authoring a file.
        private void CreateMyDataDemoFile()
        {
            var path = System.IO.Path.Combine(Application.temporaryCachePath, $"demo_{Guid.NewGuid():N}.mydata");
            System.IO.File.WriteAllText(path, "Hello from MyDataDecoder - a custom decoder registered entirely outside the package.");
            _customPathInput = path;
        }

        private async UniTaskVoid LoadCustomAsync(AssetKind kind, string pathOrUrl)
        {
            _busy = true;
            _customStatus = "Loading...";

            try
            {
                var url = ResolveUrl(pathOrUrl);
                var request = new AssetRequest(url);
                var stopwatch = Stopwatch.StartNew();

                switch (kind)
                {
                    case AssetKind.Texture2D:
                        _customTextureHandle?.Release();
                        _customTextureHandle = await _loader.LoadAsync<Texture2D>(request);
                        break;
                    case AssetKind.AudioClip:
                        _customAudioHandle?.Release();
                        _customAudioHandle = await _loader.LoadAsync<AudioClip>(request);
                        break;
                    case AssetKind.Text:
                        _customTextHandle?.Release();
                        _customTextHandle = await _loader.LoadAsync<string>(request);
                        break;
                    case AssetKind.MyData:
                        _customMyDataHandle?.Release();
                        _customMyDataHandle = await _loader.LoadAsync<MyDataAsset>(request);
                        break;
                }

                stopwatch.Stop();
                _customStatus = $"Loaded in {stopwatch.ElapsedMilliseconds}ms from: {url}";
            }
            catch (Exception ex)
            {
                _customStatus = $"Load failed: {ex.Message}";
            }
            finally
            {
                _busy = false;
                RefreshDiskStatsAsync().Forget();
            }
        }

        private string _diskStatsLine = "Disk cache: (refreshing...)";

        private async UniTaskVoid RefreshDiskStatsAsync()
        {
            var totalBytes = await _config.DiskCache.GetTotalSizeBytesAsync(CancellationToken.None);
            var entries = await _config.MetadataStore.GetAllAsync(CancellationToken.None);
            _diskStatsLine = $"Disk cache: {entries.Count} entries, {totalBytes / 1024} KB";
        }

        private async UniTaskVoid LoadOnceAsync(string url)
        {
            _busy = true;
            _status = "Loading...";

            try
            {
                var request = new AssetRequest(url);
                var ramKey = _keyProvider.GetRamKey(request);

                // release whatever this same demo slot was holding before, otherwise every click
                // pins one more handle and Evict() below could never actually destroy anything
                _currentHandle?.Release();

                var stopwatch = Stopwatch.StartNew();
                _currentHandle = await _loader.LoadAsync<Texture2D>(request);
                stopwatch.Stop();

                _currentTexture = _currentHandle.Asset;
                _ramTrackedEntries[ramKey] = (long)_currentTexture.width * _currentTexture.height * 4;
                _status = $"Loaded in {stopwatch.ElapsedMilliseconds}ms - see event log for which tier it came from.";
            }
            catch (Exception ex)
            {
                _status = $"Load failed: {ex.Message}";
            }
            finally
            {
                _busy = false;
                RefreshDiskStatsAsync().Forget();
            }
        }

        private void ClearRam()
        {
            var request = new AssetRequest(_imageUrl);
            var ramKey = _keyProvider.GetRamKey(request);

            _currentHandle?.Release();
            _currentHandle = null;
            _currentTexture = null;

            _config.RamCache.Evict(ramKey);
            _ramTrackedEntries.Remove(ramKey);

            _status = "RAM cache cleared for this URL - Load again to see a disk hit or revalidate.";
        }

        private async UniTaskVoid ClearDiskAsync()
        {
            _busy = true;
            _status = "Clearing disk cache...";

            try
            {
                var entries = await _config.MetadataStore.GetAllAsync(CancellationToken.None);
                foreach (var entry in entries)
                {
                    await _config.DiskCache.EvictAsync(entry.Key, CancellationToken.None);
                    await _config.MetadataStore.RemoveAsync(entry.Key, CancellationToken.None);
                }

                _status = $"Disk cache cleared ({entries.Count} entries) - Load again to see a full network fetch.";
            }
            finally
            {
                _busy = false;
                RefreshDiskStatsAsync().Forget();
            }
        }

        private async UniTaskVoid LoadFiveConcurrentAsync()
        {
            _busy = true;
            var before = _countingHttpClient.RequestCount;
            _status = "Loading 5 concurrent requests for the same URL...";

            try
            {
                var request = new AssetRequest(_imageUrl);
                var tasks = new UniTask<AssetHandle<Texture2D>>[5];
                for (var i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = _loader.LoadAsync<Texture2D>(request);
                }

                try
                {
                    var handles = await UniTask.WhenAll(tasks);

                    var after = _countingHttpClient.RequestCount;
                    _status = $"5 concurrent loads finished. HTTP requests made: {after - before} (should be 0 or 1, never 5) - proves dedup coalescing.";

                    foreach (var handle in handles)
                    {
                        handle.Release();
                    }
                }
                catch (Exception ex)
                {
                    // WhenAll throws on the first failure without waiting for the rest, so any
                    // task that succeeded before/alongside the failing one still needs releasing -
                    // otherwise a failed demo click quietly leaks a RAM cache entry.
                    _status = $"Concurrent load failed: {ex.Message}";
                    foreach (var task in tasks)
                    {
                        try
                        {
                            var handle = await task;
                            handle.Release();
                        }
                        catch
                        {
                            // this task's own failure, already reflected in _status above
                        }
                    }
                }
            }
            finally
            {
                _busy = false;
            }
        }

        private sealed class DemoEventSink : IAssetLoaderEventSink
        {
            private readonly DemoController _owner;
            public DemoEventSink(DemoController owner) => _owner = owner;

            public void Report(in AssetLoaderEvent loaderEvent) => _owner.OnPipelineEvent(loaderEvent);
        }
    }
}
