using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SiPV.AssetLoader
{
    /// <summary>Loads an <see cref="AssetBundle"/> from downloaded bytes.</summary>
    /// <remarks>
    /// <para>
    /// Matches the <c>unity3d</c>, <c>bundle</c>, and <c>assetbundle</c> extensions.
    /// <c>AssetBundle.LoadFromMemoryAsync</c> is a UnityEngine call, so this switches to the main
    /// thread first and the load itself costs main-thread time.
    /// </para>
    /// <para>
    /// The bundle it returns is a live Unity resource that must be unloaded, and the default
    /// releaser's <c>Object.Destroy</c> is not the right call for one. Register an
    /// <see cref="IAssetReleaser"/> that calls <c>AssetBundle.Unload</c> if you cache bundles
    /// through this loader.
    /// </para>
    /// <para>
    /// Cancellation is best-effort by necessity: Unity's bundle load cannot be aborted once
    /// started, so a cancelled decode stops waiting but keeps a background continuation alive to
    /// unload the bundle if it does finish, rather than leaking it.
    /// </para>
    /// </remarks>
    public sealed class AssetBundleDecoder : IAssetDecoder<AssetBundle>
    {
        /// <inheritdoc />
        public bool CanDecode(Type targetType, string contentTypeOrExtension)
        {
            if (targetType != typeof(AssetBundle) || string.IsNullOrEmpty(contentTypeOrExtension))
            {
                return false;
            }

            var value = contentTypeOrExtension.ToLowerInvariant();
            return value == "unity3d" || value == "bundle" || value == "assetbundle";
        }

        public async UniTask<AssetBundle> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);

            // AssetBundleCreateRequest has no Abort - cancelling cancellationToken only stop awaiting it,
            // Unity finishes loading the bundle into memory regardless.
            var loadTask = AssetBundle.LoadFromMemoryAsync(processedBytes).ToUniTask().Preserve();

            AssetBundle bundle;
            try
            {
                bundle = await loadTask.AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                UnloadIfOrphaned(loadTask).Forget();
                throw;
            }

            if (bundle == null)
            {
                throw new AssetLoadException(AssetLoadErrorCode.DecodeFailed, context.Url, "AssetBundle.LoadFromMemoryAsync returned null, bundle bytes are invalid or corrupt.");
            }

            return bundle;
        }

        private static async UniTaskVoid UnloadIfOrphaned(UniTask<AssetBundle> loadTask)
        {
            try
            {
                var bundle = await loadTask;
                if (bundle != null)
                {
                    bundle.Unload(true);
                }
            }
            catch
            {
                // the load itself failed after we'd already given up on it - nothing to unload
            }
        }
    }
}
