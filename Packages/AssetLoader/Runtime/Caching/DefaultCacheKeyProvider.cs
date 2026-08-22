using System.Security.Cryptography;
using System.Text;

namespace SiPV.AssetLoader
{
    // Default ICacheKeyProvider. RAM key is the raw seed (readable in a debugger). Disk key is
    // SHA256 hex of the same seed - mobile filesystems break on the length/characters a raw URL
    // can contain, a fixed-length hash never does.
    public sealed class DefaultCacheKeyProvider : ICacheKeyProvider
    {
        public string GetRamKey(in AssetRequest request) => request.ResolveCacheSeed();

        public string GetDiskKey(in AssetRequest request)
        {
            var seed = request.ResolveCacheSeed();

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(seed));

            var builder = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
