namespace SiPV.AssetLoader
{
    // Outcome of an IAssetSource fetch attempt.
    public enum AssetSourceStatus
    {
        // full content returned (unconditional GET, or conditional GET where content changed)
        Ok200,

        // cached ETag/Last-Modified still valid, no body returned
        NotModified304,

        Failed
    }
}
