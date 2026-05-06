namespace LocalCursorAgent.Core
{
    internal static class VerificationOutcomeBuilder
    {
        internal static VerificationOutcomeStatus BuildStatus(bool buildStarted, bool buildSucceeded)
        {
            if (!buildStarted)
            {
                return VerificationOutcomeStatus.NotStarted;
            }

            return buildSucceeded
                ? VerificationOutcomeStatus.Succeeded
                : VerificationOutcomeStatus.Failed;
        }
    }
}
