namespace SiPV.AssetLoader
{
    public interface IAssetReleaser
    {
        void Release<T>(T asset);
    }
}
