namespace SiPV.AssetLoader
{
    /// <summary>Decides whether a failed attempt should be retried, and after what delay.</summary>
    public interface IRetryPolicy
    {
        // Called once per failed attempt; return RetryDecision.Stop to give up.
        RetryDecision ShouldRetry(in RetryContext context);
    }
}
