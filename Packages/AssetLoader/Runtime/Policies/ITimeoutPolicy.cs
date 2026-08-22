using System;

namespace SiPV.AssetLoader
{
    /// <summary>Decides how long a request can run before the pipeline cancels it as timed out.</summary>
    public interface ITimeoutPolicy
    {
        // Called once before the first attempt; not re-evaluated between retries.
        TimeSpan GetTimeout(in AssetRequest request);
    }
}
