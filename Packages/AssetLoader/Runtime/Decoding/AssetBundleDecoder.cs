using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SiPV.AssetLoader
{
    // Built-in decoder for AssetBundle.
    public sealed class AssetBundleDecoder : IAssetDecoder<AssetBundle>
    {
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
