namespace SiPV.AssetLoader
{
    public interface IMemorySizeEstimator
    {
        long EstimateBytes<T>(T asset);
    }
}
