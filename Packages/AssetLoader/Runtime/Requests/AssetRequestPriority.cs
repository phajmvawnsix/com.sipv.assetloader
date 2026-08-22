namespace SiPV.AssetLoader
{
    /// <summary>Relative scheduling priority for queuing/throttling fetches; no effect on RAM cache hits.</summary>
    public enum AssetRequestPriority
    {
        Low,
        Normal,
        High,
        Immediate
    }
}
