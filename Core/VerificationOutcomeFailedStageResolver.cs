namespace LocalCursorAgent.Core
{
    internal static class VerificationOutcomeFailedStageResolver
    {
        internal static string Resolve(string? failedStage)
        {
            return failedStage ?? string.Empty;
        }
    }
}
