using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SiPV.AssetLoader
{
    // Built-in decoder for AudioClip, via UnityWebRequestMultimedia - no external library needed,
    // it's part of UnityEngine.Networking same as HttpAssetSource's transport.
    public sealed class AudioClipDecoder : IAssetDecoder<AudioClip>
    {
        public bool CanDecode(Type targetType, string contentTypeOrExtension)
        {
            if (targetType != typeof(AudioClip) || string.IsNullOrEmpty(contentTypeOrExtension))
            {
                return false;
            }

            var value = contentTypeOrExtension.ToLowerInvariant();
            return value == "wav" || value == "ogg" || value == "mp3" || value.StartsWith("audio/");
        }

        public async UniTask<AudioClip> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);

            var audioType = ResolveAudioType(context);
            var tempPath = Path.Combine(Application.temporaryCachePath, $"sipv_assetloader_audio_{Guid.NewGuid():N}");
            File.WriteAllBytes(tempPath, processedBytes);

            try
            {
                using var webRequest = UnityWebRequestMultimedia.GetAudioClip(new Uri(tempPath).AbsoluteUri, audioType);
                var operation = webRequest.SendWebRequest();

                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        webRequest.Abort();
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    await UniTask.Yield();
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new AssetLoadException(AssetLoadErrorCode.DecodeFailed, context.Url, $"AudioClipDecoder: UnityWebRequestMultimedia failed: {webRequest.error}");
                }

                var clip = DownloadHandlerAudioClip.GetContent(webRequest);
                if (clip == null)
                {
                    throw new AssetLoadException(AssetLoadErrorCode.DecodeFailed, context.Url, "AudioClipDecoder: DownloadHandlerAudioClip returned null, audio bytes are invalid or corrupt.");
                }

                return clip;
            }
            finally
            {
                TryDeleteTempFile(tempPath);
            }
        }

        // AudioType.MPEG works via UnityWebRequestMultimedia on desktop/Editor but isn't guaranteed on every platform - Unity's own docs flag mp3 decode support as inconsistent.
        private static AudioType ResolveAudioType(AssetDecodeContext context)
        {
            var value = (!string.IsNullOrEmpty(context.Extension) ? context.Extension : context.ContentType)?.ToLowerInvariant() ?? string.Empty;

            if (value.Contains("wav"))
            {
                return AudioType.WAV;
            }

            if (value.Contains("ogg") || value.Contains("vorbis"))
            {
                return AudioType.OGGVORBIS;
            }

            if (value.Contains("mp3") || value.Contains("mpeg"))
            {
                return AudioType.MPEG;
            }

            throw new AssetLoadException(
                AssetLoadErrorCode.DecodeFailed, context.Url,
                $"AudioClipDecoder: could not determine an AudioType from extension '{context.Extension}' / content type '{context.ContentType}'.");
        }

        private static void TryDeleteTempFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
