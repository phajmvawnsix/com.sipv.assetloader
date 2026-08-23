namespace SiPV.AssetLoader
{
    /// <summary>Pipeline stage a retry decision is being made for.</summary>
    public enum RetryStage
    {
        /// <summary>The source fetch (network request) failed.</summary>
        Fetch,

        /// <summary>A content processor in the chain failed.</summary>
        Process,

        /// <summary>The decoder failed to produce an asset from the bytes.</summary>
        Decode
    }
}
