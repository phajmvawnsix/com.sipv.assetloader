using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SiPV.AssetLoader
{
    /// <summary>Decodes WAV, OGG, or MP3 bytes into an <see cref="AudioClip"/>.</summary>
    /// <remarks>
    /// <para>
    /// Uses <see cref="UnityWebRequestMultimedia"/>, part of <c>UnityEngine.Networking</c> like
    /// <see cref="HttpAssetSource"/>'s own transport, so no external decode library is needed.
    /// Matches <c>audio/*</c> content types and the <c>wav</c>, <c>ogg</c>, and <c>mp3</c>
    /// extensions.
    /// </para>
    /// <para>
    /// <see cref="UnityWebRequestMultimedia"/> only reads from a URI, not from an in-memory
    /// buffer, so the processed bytes are written to a temp file under
    /// <see cref="Application.temporaryCachePath"/> first and deleted again in a
    /// <c>finally</c> block regardless of success, failure, or cancellation.
    /// </para>
    /// <para>
    /// <c>AudioType.MPEG</c> decode support is inconsistent across platforms per Unity's own
    /// documentation: verify mp3 playback on every target platform rather than assuming desktop
    /// or Editor behavior carries over.
    /// </para>
    /// </remarks>
    public sealed class AudioClipDecoder : IAssetDecoder<AudioClip>
    {
        /// <inheritdoc />
        public bool CanDecode(Type targetType, string contentTypeOrExtension)
        {
            if (targetType != typeof(AudioClip) || string.IsNullOrEmpty(contentTypeOrExtension))
            {
                return false;
            }

            var value = contentTypeOrExtension.ToLowerInvariant();
            return value == "wav" || value == "ogg" || value == "mp3" || value.StartsWith("audio/");
        }

        /// <inheritdoc />
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

                // GetContent doesn't return null on a native decode failure.
                if (clip == null || clip.samples <= 0)
                {
                    throw new AssetLoadException(AssetLoadErrorCode.DecodeFailed, context.Url, "AudioClipDecoder: audio bytes are invalid or corrupt (native decode failed).");
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
