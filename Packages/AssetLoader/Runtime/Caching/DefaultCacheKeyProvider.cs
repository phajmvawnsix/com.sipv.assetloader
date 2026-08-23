using System.Security.Cryptography;
using System.Text;

namespace SiPV.AssetLoader
{
    /// <summary>
    /// Keys straight off the request's cache seed: raw for RAM, SHA256 hex for disk.
    /// </summary>
    /// <remarks>
    /// The RAM key stays raw because it is only ever a dictionary key, and a readable one is far
    /// easier to follow in a debugger or a log. The disk key must survive being a filename on every
    /// target platform, which a raw URL does not: mobile filesystems reject characters URLs use
    /// freely and impose path length limits URLs routinely exceed. A fixed-length hash sidesteps
    /// both. Subclass or replace this to add cache-busting, for example prefixing the seed with an
    /// app or content version so a release invalidates everything at once.
    /// </remarks>
    public sealed class DefaultCacheKeyProvider : ICacheKeyProvider
    {
        /// <inheritdoc />
        public string GetRamKey(in AssetRequest request) => request.ResolveCacheSeed();

        /// <inheritdoc />
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
