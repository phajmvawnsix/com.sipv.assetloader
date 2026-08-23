using System;
using System.Collections.Generic;

namespace SiPV.AssetLoader.Tests
{
    internal sealed class FakeAssetReleaser : IAssetReleaser
    {
        public readonly List<object> Released = new List<object>();
        public void Release<T>(T asset) => Released.Add(asset);
    }

    internal sealed class FakeMemorySizeEstimator : IMemorySizeEstimator
    {
        public Func<object, long> Estimate = _ => 1;
        public long EstimateBytes<T>(T asset) => Estimate(asset);
    }

    internal sealed class FakeAssetLoaderLogger : IAssetLoaderLogger
    {
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public void LogWarning(string message) => Warnings.Add(message);
        public void LogError(string message) => Errors.Add(message);
    }
}
