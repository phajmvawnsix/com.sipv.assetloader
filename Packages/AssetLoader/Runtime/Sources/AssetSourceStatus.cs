namespace SiPV.AssetLoader
{
    /// <summary>Outcome of an <see cref="IAssetSource"/> fetch attempt.</summary>
    public enum AssetSourceStatus
    {
        /// <summary>Full content returned, either an unconditional GET or a conditional GET where the content changed.</summary>
        Ok200,

        /// <summary>The cached ETag or Last-Modified value is still valid; no body was returned.</summary>
        NotModified304,

        /// <summary>The fetch failed; see <see cref="AssetSourceResult.Error"/>.</summary>
        Failed
    }
}
