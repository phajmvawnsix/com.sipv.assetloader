using UnityEngine;

namespace SiPV.AssetLoader
{
    // Default IMemorySizeEstimator. Deliberately rough:
    //   Texture2D  -> width * height * 4 (relative number, not 100% accurate, but good enough to avoid OOMs)
    //   AudioClip  -> samples * channels * 4 (assumes float PCM in memory, same caveat)
    //   byte[]     -> exact length
    //   string     -> Length * 2 (default as UTF-16)
    //   anything else -> 1, so it still counts toward an entry-count budget without dominating a
    //                 byte budget it can't meaningfully estimate
    public sealed class DefaultMemorySizeEstimator : IMemorySizeEstimator
    {
        public long EstimateBytes<T>(T asset)
        {
            switch (asset)
            {
                case null:
                    return 0;
                case Texture2D texture:
                    return (long)texture.width * texture.height * 4;
                case AudioClip clip:
                    return (long)clip.samples * clip.channels * 4;
                case byte[] bytes:
                    return bytes.Length;
                case string text:
                    return text.Length * 2;
                default:
                    return 1;
            }
        }
    }
}
