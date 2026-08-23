using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SiPV.AssetLoader.Tests
{
    public class AudioClipDecoderTests
    {
        private static AssetDecodeContext Context(string extension = "", string contentType = "") =>
            new AssetDecodeContext("http://example.com/asset", contentType, extension, null);

        // Minimal valid 16-bit PCM mono WAV, hand-built rather than pulled from a test asset -
        // Unity has no WAV-encode API the way Texture2D.EncodeToPNG exists for images.
        private static byte[] BuildWavBytes(int sampleRate, int sampleCount)
        {
            const int bitsPerSample = 16;
            const int channels = 1;
            var dataSize = sampleCount * channels * (bitsPerSample / 8);
            var bytes = new List<byte>(44 + dataSize);

            void WriteAscii(string value) => bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(value));
            void WriteInt32(int value) => bytes.AddRange(BitConverter.GetBytes(value));
            void WriteInt16(short value) => bytes.AddRange(BitConverter.GetBytes(value));

            WriteAscii("RIFF");
            WriteInt32(36 + dataSize);
            WriteAscii("WAVE");
            WriteAscii("fmt ");
            WriteInt32(16);
            WriteInt16(1); // PCM
            WriteInt16(channels);
            WriteInt32(sampleRate);
            WriteInt32(sampleRate * channels * (bitsPerSample / 8));
            WriteInt16((short)(channels * (bitsPerSample / 8)));
            WriteInt16(bitsPerSample);
            WriteAscii("data");
            WriteInt32(dataSize);

            for (var i = 0; i < sampleCount; i++)
            {
                WriteInt16(0); // silence
            }

            return bytes.ToArray();
        }

        [Test]
        public void CanDecode_MatchesKnownAudioExtensionsAndContentType()
        {
            var decoder = new AudioClipDecoder();

            Assert.IsTrue(decoder.CanDecode(typeof(AudioClip), "wav"));
            Assert.IsTrue(decoder.CanDecode(typeof(AudioClip), "ogg"));
            Assert.IsTrue(decoder.CanDecode(typeof(AudioClip), "audio/mpeg"));
            Assert.IsFalse(decoder.CanDecode(typeof(AudioClip), "png"));
        }

        [UnityTest]
        public IEnumerator DecodeAsync_ValidWavBytes_DecodesSuccessfully() => RunAsync(async () =>
        {
            var wavBytes = BuildWavBytes(sampleRate: 8000, sampleCount: 400);
            var decoder = new AudioClipDecoder();

            var clip = await decoder.DecodeAsync(wavBytes, Context("wav", "audio/wav"), CancellationToken.None);

            Assert.IsNotNull(clip);
            Assert.AreEqual(8000, clip.frequency);
            Assert.AreEqual(1, clip.channels);
            Assert.Greater(clip.samples, 0);
            UnityEngine.Object.DestroyImmediate(clip);
        });

        [UnityTest]
        public IEnumerator DecodeAsync_GarbageBytes_ThrowsDecodeFailed() => RunAsync(async () =>
        {
            var decoder = new AudioClipDecoder();
            var garbage = new byte[] { 1, 2, 3, 4, 5 };

            // The garbage bytes still pass the file-read/UnityWebRequest.result check (the temp
            // file itself was written fine) - decode failure only surfaces when
            // DownloadHandlerAudioClip.GetContent hands the bytes to the native audio backend
            // (FMOD), which logs its own Error to the console before returning null. Unity Test
            // Framework auto-fails a test on any unexpected Error-level log, so this one has to be
            // told to expect it - the actual behavior under test is still the AssetLoadException
            // assertion below, this just stops an unrelated native log line from failing the test.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));

            AssetLoadException caught = null;
            try
            {
                await decoder.DecodeAsync(garbage, Context("wav", "audio/wav"), CancellationToken.None);
            }
            catch (AssetLoadException ex)
            {
                caught = ex;
            }

            Assert.IsNotNull(caught);
            Assert.AreEqual(AssetLoadErrorCode.DecodeFailed, caught.ErrorCode);
        });

        [UnityTest]
        public IEnumerator DecodeAsync_UnrecognizedExtension_ThrowsDecodeFailed() => RunAsync(async () =>
        {
            // This project's bundled NUnit has no Assert.ThrowsAsync - same reason every other
            // async failure-path test in this repo uses a manual try/catch instead.
            var decoder = new AudioClipDecoder();

            AssetLoadException caught = null;
            try
            {
                await decoder.DecodeAsync(new byte[] { 1 }, Context("xyz", "application/unknown"), CancellationToken.None);
            }
            catch (AssetLoadException ex)
            {
                caught = ex;
            }

            Assert.IsNotNull(caught);
            Assert.AreEqual(AssetLoadErrorCode.DecodeFailed, caught.ErrorCode);
        });

        private static IEnumerator RunAsync(Func<UniTask> testBody) => testBody().ToCoroutine();
    }
}
