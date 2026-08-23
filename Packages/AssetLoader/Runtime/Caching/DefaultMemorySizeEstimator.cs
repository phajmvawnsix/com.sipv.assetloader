using UnityEngine;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Rough size estimates for the common asset types, good enough to keep a cache budget honest
    /// without walking object graphs.
    /// </summary>
    /// <remarks>
    /// Deliberately approximate. A texture is counted as width times height times 4 bytes, ignoring
    /// compression and mip levels; an audio clip as samples times channels times 4, assuming float
    /// PCM. Both can be off by a factor for compressed assets, but they are proportional to real
    /// cost, which is what a budget needs. Byte arrays and strings are exact. Anything else counts
    /// as 1 byte, so it still contributes to an entry-count budget without distorting a byte
    /// budget it cannot meaningfully estimate: register your own
    /// <see cref="IMemorySizeEstimator"/> if that matters for your type.
    /// </remarks>
    public sealed class DefaultMemorySizeEstimator : IMemorySizeEstimator
    {
        /// <inheritdoc />
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
