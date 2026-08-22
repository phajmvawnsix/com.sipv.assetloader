using System;
using System.Collections.Generic;

namespace SiPV.AssetLoader
{
    // Default IAssetDecoderRegistry. Not thread-safe - registration is at bootstrap, resolution during
    // pipeline execution, both main-thread only.
    public sealed class AssetDecoderRegistry : IAssetDecoderRegistry
    {
        private readonly Dictionary<Type, List<object>> _decodersByType = new Dictionary<Type, List<object>>();

        public void Register<T>(IAssetDecoder<T> decoder)
        {
            if (decoder == null)
            {
                throw new ArgumentNullException(nameof(decoder));
            }

            var type = typeof(T);
            if (!_decodersByType.TryGetValue(type, out var list))
            {
                list = new List<object>();
                _decodersByType[type] = list;
            }

            // Insert at front so TryResolve checks newest first - last-registered wins on conflict.
            list.Insert(0, decoder);
        }

        public bool TryResolve<T>(string contentType, string extension, out IAssetDecoder<T> decoder)
        {
            decoder = null;
            var type = typeof(T);

            if (!_decodersByType.TryGetValue(type, out var list))
            {
                return false;
            }

            // TODO: two CanDecode calls per candidate is wasteful, and it silently lets a decoder match on
            // extension when the content type says otherwise. Probably wants contentType to win outright.
            foreach (var candidate in list)
            {
                var typed = (IAssetDecoder<T>)candidate;
                if (typed.CanDecode(type, contentType) || typed.CanDecode(type, extension))
                {
                    decoder = typed;
                    return true;
                }
            }

            return false;
        }
    }
}
