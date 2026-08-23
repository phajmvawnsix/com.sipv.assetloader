using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Entry point of the package: an async facade over the load pipeline (dedup, RAM cache, disk
    /// cache, source fetch, content processing, decode). Build instances via
    /// <see cref="AssetLoaderConfigBuilder"/>.
    /// </summary>
    public interface IAssetLoader
    {
        /// <summary>
        /// Loads an asset, going through the cache tiers before hitting the source.
        /// Call from the main thread; it always resumes there too, so the returned handle's
        /// Asset can be used immediately without re-syncing.
        /// </summary>
        /// <typeparam name="T">
        /// Target asset type. A decoder for this type must be registered via
        /// <see cref="AssetLoaderConfigBuilder.RegisterDecoder{T}"/>, otherwise the load fails with
        /// <see cref="AssetLoadErrorCode.DecodeFailed"/>.
        /// </typeparam>
        /// <param name="request">What to load, plus any per-request policy overrides.</param>
        /// <param name="cancellationToken">
        /// Cancels this caller's wait. If other callers are coalesced onto the same in-flight load,
        /// the underlying fetch keeps running for them: it is only abandoned once every waiter has
        /// cancelled.
        /// </param>
        /// <returns>
        /// A ref-counted handle the caller owns. Release it when done or the asset stays pinned
        /// in the RAM cache for the rest of the session.
        /// </returns>
        /// <exception cref="AssetLoadException">
        /// Thrown when the load fails. Branch on <see cref="AssetLoadException.ErrorCode"/> rather
        /// than parsing the message.
        /// </exception>
        /// <exception cref="System.OperationCanceledException">
        /// Thrown when <paramref name="cancellationToken"/> fires, or when a policy timeout elapses.
        /// </exception>
        UniTask<AssetHandle<T>> LoadAsync<T>(AssetRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Warms the disk cache without decoding or handing back a handle. Good for prefetching a
        /// batch of assets during a loading screen so later <see cref="LoadAsync{T}"/> calls skip
        /// the network.
        /// </summary>
        /// <param name="request">What to fetch. Policy overrides apply the same as for a load.</param>
        /// <param name="cancellationToken">Cancels the prefetch.</param>
        /// <remarks>
        /// Deliberately not generic: with no target type there is nothing to decode and nothing to
        /// put in the RAM cache, so this stops once the bytes are on disk.
        /// </remarks>
        UniTask PreloadAsync(AssetRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Drops this request's entry from both cache tiers, so the next load refetches from the
        /// source.
        /// </summary>
        /// <param name="request">
        /// The full request, not just a key, so RAM and disk keys are derived exactly the way
        /// <see cref="LoadAsync{T}"/> would derive them. Passing a request whose Key or Variant
        /// differs would invalidate a different entry.
        /// </param>
        /// <param name="cancellationToken">Cancels the disk-side eviction.</param>
        /// <remarks>
        /// Handles already issued to callers stay valid and usable until released. This only
        /// removes the cache entries so nothing new resolves to them.
        /// </remarks>
        UniTask InvalidateAsync(AssetRequest request, CancellationToken cancellationToken = default);
    }
}
