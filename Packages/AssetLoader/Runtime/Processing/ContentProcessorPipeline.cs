using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    // runs processors in order, output feeds next input; empty list = identity
    public sealed class ContentProcessorPipeline : IContentProcessorPipeline
    {
        private readonly IReadOnlyList<IContentProcessor> _processors;

        public ContentProcessorPipeline(IReadOnlyList<IContentProcessor> processors)
        {
            _processors = processors ?? System.Array.Empty<IContentProcessor>();
        }

        public async UniTask<byte[]> RunAsync(byte[] rawBytes, AssetProcessingContext context, CancellationToken cancellationToken)
        {
            var current = rawBytes;

            for (var i = 0; i < _processors.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                current = await _processors[i].ProcessAsync(current, context, cancellationToken);
            }

            return current;
        }
    }
}
