using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>Decodes bytes as UTF8 text.</summary>
    /// <remarks>
    /// Matches on the target type alone, so it accepts any content type or extension: JSON, XML,
    /// CSV, and plain text all decode the same way. Assumes UTF8 with no other encodings attempted,
    /// and does no BOM handling beyond what <see cref="Encoding.UTF8"/> does natively. Register
    /// your own <c>IAssetDecoder&lt;string&gt;</c> if you need a different encoding.
    /// </remarks>
    public sealed class StringDecoder : IAssetDecoder<string>
    {
        /// <inheritdoc />
        public bool CanDecode(Type targetType, string contentTypeOrExtension) => targetType == typeof(string);

        /// <inheritdoc />
        public UniTask<string> DecodeAsync(byte[] processedBytes, AssetDecodeContext context, CancellationToken cancellationToken) =>
            UniTask.FromResult(Encoding.UTF8.GetString(processedBytes));
    }
}
