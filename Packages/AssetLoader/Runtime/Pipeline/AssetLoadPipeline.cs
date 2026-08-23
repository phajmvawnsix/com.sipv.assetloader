using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // Default IAssetLoadPipeline: dedup -> RAM -> disk+freshness -> fetch -> process -> decode -> populate.
    // Owned internally by AssetLoaderService, never exposed to consumers.
    internal sealed class AssetLoadPipeline : IAssetLoadPipeline
    {
        private readonly AssetLoaderConfig _config;
        private readonly IInFlightRequestCoordinator _coordinator;

        internal AssetLoadPipeline(AssetLoaderConfig config, IInFlightRequestCoordinator coordinator)
        {
            _config = config;
            _coordinator = coordinator;
        }

        public async UniTask<AssetHandle<T>> ExecuteAsync<T>(AssetRequest request, CancellationToken cancellationToken)
        {
            var ramKey = _config.CacheKeyProvider.GetRamKey(request);

            if (!request.Flags.HasFlag(AssetRequestFlags.BypassRamCache)
                && _config.RamCache.TryGet<T>(ramKey, out var ramHandle))
            {
                _config.EventSink.Report(new AssetLoaderEvent(AssetLoaderEventKind.RamCacheHit, ramKey));
                return ramHandle;
            }

            if (_coordinator.TryGetExisting<T>(ramKey, cancellationToken, out var existing))
            {
                _config.EventSink.Report(new AssetLoaderEvent(AssetLoaderEventKind.DedupCoalesced, ramKey));
                var sharedHandle = await AwaitOwnCancellation(existing, cancellationToken);
                return sharedHandle.Retain();
            }

            var completionSource = _coordinator.Register<T>(ramKey, cancellationToken, out var sharedToken);


            var workTask = LoadAndCacheAsync<T>(request, ramKey, sharedToken);
            ResolveForCoalescers(workTask, completionSource, ramKey).Forget();

            return await AwaitOwnCancellation(completionSource.Task, cancellationToken);
        }
        
        private async UniTaskVoid ResolveForCoalescers<T>(
            UniTask<AssetHandle<T>> workTask, UniTaskCompletionSource<AssetHandle<T>> completionSource, string ramKey)
        {
            try
            {
                completionSource.TrySetResult(await workTask);
            }
            catch (Exception ex)
            {
                completionSource.TrySetException(ex);
                
                try
                {
                    completionSource.Task.GetAwaiter().GetResult();
                }
                catch
                {
                }
            }
            finally
            {
                _coordinator.Complete(ramKey, completionSource);
            }
        }
        
        private static UniTask<AssetHandle<T>> AwaitOwnCancellation<T>(UniTask<AssetHandle<T>> task, CancellationToken callerToken) =>
            task.AttachExternalCancellation(callerToken);

        public async UniTask PreloadAsync(AssetRequest request, CancellationToken cancellationToken)
        {
            var ramKey = _config.CacheKeyProvider.GetRamKey(request);
            var diskKey = _config.CacheKeyProvider.GetDiskKey(request);

            await UniTask.SwitchToThreadPool();

            CacheEntryMetadata? existingMetadata = null;
            var lookup = CacheLookupResult.Miss;

            if (!request.Flags.HasFlag(AssetRequestFlags.BypassDiskCache))
            {
                existingMetadata = await _config.MetadataStore.GetAsync(diskKey, cancellationToken);
                var cachePolicy = request.CachePolicyOverride ?? _config.DefaultCachePolicy;
                lookup = cachePolicy.Evaluate(request, existingMetadata, DateTimeOffset.UtcNow);
            }

            if (lookup == CacheLookupResult.Fresh)
            {
                await _config.MetadataStore.SetAsync(diskKey, existingMetadata.Value.WithLastAccess(DateTimeOffset.UtcNow), cancellationToken);
                return;
            }

            var fetchResult = await FetchWithRetryAsync(request, ramKey, existingMetadata, cancellationToken);

            if (fetchResult.Status == AssetSourceStatus.NotModified304)
            {
                if (!existingMetadata.HasValue)
                {
                    throw new AssetLoadException(
                        AssetLoadErrorCode.FetchFailed, ramKey,
                        "Source returned 304 Not Modified with no known prior metadata to revalidate against.");
                }

                var refreshed = existingMetadata.Value.WithLastAccess(DateTimeOffset.UtcNow);
                await _config.MetadataStore.SetAsync(diskKey, refreshed, cancellationToken);
                return;
            }

            var newMetadata = new CacheEntryMetadata(
                fetchResult.ETag, fetchResult.MaxAge, fetchResult.FetchedAtUtc,
                fetchResult.FetchedAtUtc, fetchResult.RawBytes.Length, fetchResult.ContentType);

            await _config.DiskCache.WriteAsync(diskKey, fetchResult.RawBytes, cancellationToken);
            await _config.MetadataStore.SetAsync(diskKey, newMetadata, cancellationToken);
        }

        private async UniTask<AssetHandle<T>> LoadAndCacheAsync<T>(AssetRequest request, string ramKey, CancellationToken cancellationToken)
        {
            await UniTask.SwitchToThreadPool();

            var diskKey = _config.CacheKeyProvider.GetDiskKey(request);
            var bypassDisk = request.Flags.HasFlag(AssetRequestFlags.BypassDiskCache);

            CacheEntryMetadata? existingMetadata = null;
            var lookup = CacheLookupResult.Miss;

            if (!bypassDisk)
            {
                existingMetadata = await _config.MetadataStore.GetAsync(diskKey, cancellationToken);
                var cachePolicy = request.CachePolicyOverride ?? _config.DefaultCachePolicy;
                lookup = cachePolicy.Evaluate(request, existingMetadata, DateTimeOffset.UtcNow);
            }

            byte[] rawBytes = null;
            var finalMetadata = default(CacheEntryMetadata);

            if (lookup == CacheLookupResult.Fresh)
            {
                var readResult = await _config.DiskCache.TryReadAsync(diskKey, cancellationToken);
                if (readResult.Found)
                {
                    rawBytes = readResult.Content;
                    finalMetadata = existingMetadata.Value.WithLastAccess(DateTimeOffset.UtcNow);
                    await _config.MetadataStore.SetAsync(diskKey, finalMetadata, cancellationToken);
                    _config.EventSink.Report(new AssetLoaderEvent(AssetLoaderEventKind.DiskCacheHit, ramKey));
                }
                // metadata said fresh but the bytes are gone - fall through to a full fetch below,
                // still passing along the known ETag for a best-effort conditional GET
            }

            if (rawBytes == null)
            {
                var fetchResult = await FetchWithRetryAsync(request, ramKey, existingMetadata, cancellationToken);

                if (fetchResult.Status == AssetSourceStatus.NotModified304)
                {
                    if (!existingMetadata.HasValue)
                    {
                        throw new AssetLoadException(
                            AssetLoadErrorCode.FetchFailed, ramKey,
                            "Source returned 304 Not Modified with no known prior metadata to revalidate against.");
                    }

                    var readResult = await _config.DiskCache.TryReadAsync(diskKey, cancellationToken);
                    if (!readResult.Found)
                    {
                        throw new AssetLoadException(
                            AssetLoadErrorCode.FetchFailed, ramKey,
                            "Source returned 304 Not Modified but the disk cache no longer has the content.");
                    }

                    rawBytes = readResult.Content;
                    finalMetadata = existingMetadata.Value.WithLastAccess(DateTimeOffset.UtcNow);
                    if (fetchResult.MaxAge.HasValue)
                    {
                        finalMetadata = new CacheEntryMetadata(
                            finalMetadata.ETag, fetchResult.MaxAge, finalMetadata.FetchedAtUtc,
                            finalMetadata.LastAccessUtc, finalMetadata.SizeBytes, finalMetadata.ContentType);
                    }

                    await _config.MetadataStore.SetAsync(diskKey, finalMetadata, cancellationToken);
                    _config.EventSink.Report(new AssetLoaderEvent(AssetLoaderEventKind.DiskCacheRevalidated, ramKey));
                }
                else
                {
                    rawBytes = fetchResult.RawBytes;
                    finalMetadata = new CacheEntryMetadata(
                        fetchResult.ETag, fetchResult.MaxAge, fetchResult.FetchedAtUtc,
                        fetchResult.FetchedAtUtc, rawBytes.Length, fetchResult.ContentType);

                    // still written even with BypassDiskCache set - bypass only skips the read side
                    await _config.DiskCache.WriteAsync(diskKey, rawBytes, cancellationToken);
                    await _config.MetadataStore.SetAsync(diskKey, finalMetadata, cancellationToken);
                    _config.EventSink.Report(new AssetLoaderEvent(AssetLoaderEventKind.CacheMiss, ramKey));
                }
            }

            var processed = await ExecuteWithRetryAsync(
                request, ramKey, RetryStage.Process, cancellationToken,
                ct => _config.ProcessorPipeline.RunAsync(
                    rawBytes, new AssetProcessingContext(request.Url, finalMetadata.ContentType, request.UserData), ct));

            var extension = ExtractExtension(request.Url);
            if (!_config.DecoderRegistry.TryResolve<T>(finalMetadata.ContentType, extension, out var decoder))
            {
                throw new AssetLoadException(
                    AssetLoadErrorCode.DecodeFailed, ramKey,
                    $"No decoder registered for {typeof(T).Name} (content-type '{finalMetadata.ContentType}', extension '{extension}').");
            }

            var decoded = await ExecuteWithRetryAsync(
                request, ramKey, RetryStage.Decode, cancellationToken,
                ct => decoder.DecodeAsync(
                    processed, new AssetDecodeContext(request.Url, finalMetadata.ContentType, extension, request.UserData), ct));

            // decoder contract guarantees main thread on completion; switch is a defensive no-op if so
            await UniTask.SwitchToMainThread();
            return _config.RamCache.Put(ramKey, decoded, finalMetadata);
        }

        private async UniTask<AssetSourceResult> FetchWithRetryAsync(
            AssetRequest request, string ramKey, CacheEntryMetadata? existingMetadata, CancellationToken cancellationToken)
        {
            return await ExecuteWithRetryAsync(request, ramKey, RetryStage.Fetch, cancellationToken, async ct =>
            {
                await UniTask.SwitchToMainThread();

                var context = new AssetSourceRequestContext(
                    request.Url,
                    existingMetadata?.ETag,
                    null, // CacheEntryMetadata doesn't persist a server Last-Modified field, ETag is the only revalidation path
                    request.CustomHeaders,
                    request.TimeoutPolicyOverride ?? _config.DefaultTimeoutPolicy,
                    request.Priority);

                var result = await _config.Source.FetchAsync(context, ct);

                await UniTask.SwitchToThreadPool();

                if (result.Status == AssetSourceStatus.Failed)
                {
                    throw result.Error ?? new AssetLoadException(
                        AssetLoadErrorCode.FetchFailed, ramKey, "Source reported failure without an exception.");
                }

                return result;
            });
        }

        // Shared timeout+retry wrapper for the Fetch/Process/Decode stages. Timeout is read once and
        // reapplied per attempt (ITimeoutPolicy.GetTimeout doc: "not re-evaluated between retries").
        private async UniTask<TResult> ExecuteWithRetryAsync<TResult>(
            AssetRequest request, string ramKey, RetryStage stage, CancellationToken cancellationToken,
            Func<CancellationToken, UniTask<TResult>> operation)
        {
            var retryPolicy = request.RetryPolicyOverride ?? _config.DefaultRetryPolicy;
            var timeoutPolicy = request.TimeoutPolicyOverride ?? _config.DefaultTimeoutPolicy;
            var timeout = timeoutPolicy.GetTimeout(request);
            var overallDeadline = timeoutPolicy.GetOverallDeadline(request);
            var startedAt = DateTimeOffset.UtcNow;
            var attempt = 1;

            while (true)
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (timeout > TimeSpan.Zero)
                {
                    attemptCts.CancelAfter(timeout);
                }

                TimeSpan? retryDelay;

                try
                {
                    return await operation(attemptCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // attemptCts fired from the timeout, not the caller's own token
                    var timeoutFailure = new AssetLoadException(AssetLoadErrorCode.TimedOut, ramKey, $"{stage} timed out after {timeout}.");
                    if (!TryGetRetryDelay(request, ramKey, stage, attempt, retryPolicy, timeoutFailure, out retryDelay))
                    {
                        throw timeoutFailure;
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    if (!TryGetRetryDelay(request, ramKey, stage, attempt, retryPolicy, ex, out retryDelay))
                    {
                        throw stage == RetryStage.Fetch && ex is AssetLoadException fetchFailure
                            ? fetchFailure
                            : new AssetLoadException(ErrorCodeFor(stage), ramKey, ex.Message, ex);
                    }
                }

                if (overallDeadline.HasValue && DateTimeOffset.UtcNow - startedAt >= overallDeadline.Value)
                {
                    throw new AssetLoadException(
                        AssetLoadErrorCode.TimedOut, ramKey, $"{stage} exceeded overall deadline of {overallDeadline.Value} across retries.");
                }

                await UniTask.Delay(retryDelay.Value, cancellationToken: cancellationToken);
                attempt++;
            }
        }

        private bool TryGetRetryDelay(
            AssetRequest request, string ramKey, RetryStage stage, int attempt, IRetryPolicy retryPolicy,
            Exception failure, out TimeSpan? delay)
        {
            var decision = retryPolicy.ShouldRetry(new RetryContext(request, attempt, failure, stage));
            if (!decision.ShouldRetry)
            {
                delay = null;
                return false;
            }

            _config.EventSink.Report(new AssetLoaderEvent(AssetLoaderEventKind.RetryAttempted, ramKey));
            delay = decision.Delay;
            return true;
        }

        private static AssetLoadErrorCode ErrorCodeFor(RetryStage stage) => stage switch
        {
            RetryStage.Process => AssetLoadErrorCode.ProcessingFailed,
            RetryStage.Decode => AssetLoadErrorCode.DecodeFailed,
            _ => AssetLoadErrorCode.FetchFailed
        };

        // Lowercase, no leading dot, ignores query string/fragment - matches AssetDecodeContext.Extension doc.
        private static string ExtractExtension(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            var end = url.Length;
            for (var i = 0; i < url.Length; i++)
            {
                if (url[i] == '?' || url[i] == '#')
                {
                    end = i;
                    break;
                }
            }

            var lastDot = -1;
            var lastSlash = -1;
            for (var i = 0; i < end; i++)
            {
                if (url[i] == '.') lastDot = i;
                else if (url[i] == '/') lastSlash = i;
            }

            return lastDot > lastSlash && lastDot < end - 1
                ? url.Substring(lastDot + 1, end - lastDot - 1).ToLowerInvariant()
                : string.Empty;
        }
    }
}
