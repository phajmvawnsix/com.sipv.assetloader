namespace SiPV.AssetLoader
{
    /// <summary>
    /// Relative scheduling hint passed through to the <see cref="IAssetSource"/>. Has no effect on
    /// cache hits, which never queue.
    /// </summary>
    /// <remarks>
    /// The built-in <see cref="HttpAssetSource"/> forwards this but does not itself queue or
    /// throttle, so today the value only matters to a custom source that implements prioritisation.
    /// </remarks>
    public enum AssetRequestPriority
    {
        /// <summary>Background work: prefetching, speculative loads.</summary>
        Low,

        /// <summary>Default.</summary>
        Normal,

        /// <summary>Something the player is waiting on, for example the current screen's content.</summary>
        High,

        /// <summary>Blocking work that should jump any queue, for example a splash screen asset.</summary>
        Immediate
    }
}
