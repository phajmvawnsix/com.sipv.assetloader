namespace SiPV.AssetLoader
{
    /// <summary>Coarse failure category for AssetLoadException, so callers can branch on an enum instead of type-checking.</summary>
    public enum AssetLoadErrorCode
    {
        Unknown,

        // network error, non-2xx/304, retries exhausted
        FetchFailed,

        // a content processor threw
        ProcessingFailed,

        // no decoder resolved, or decoder threw
        DecodeFailed,

        TimedOut,

        Cancelled
    }
}
