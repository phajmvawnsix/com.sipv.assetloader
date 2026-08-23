using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SiPV.AssetLoader
{
    /// <summary>Default <see cref="IContentProcessorPipeline"/>: runs processors in registration order, each output feeding the next input.</summary>
    /// <remarks>An empty processor list is the identity transform: the input bytes come back unchanged.</remarks>
    public sealed class ContentProcessorPipeline : IContentProcessorPipeline
    {
        private readonly IReadOnlyList<IContentProcessor> _processors;

        /// <summary>Creates a pipeline over the given processors, run in list order.</summary>
        /// <param name="processors">The chain to run. Null is treated as an empty chain.</param>
        public ContentProcessorPipeline(IReadOnlyList<IContentProcessor> processors)
        {
            _processors = processors ?? System.Array.Empty<IContentProcessor>();
        }

        /// <inheritdoc />
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
