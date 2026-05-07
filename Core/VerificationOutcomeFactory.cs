namespace LocalCursorAgent.Core
{
    internal static class VerificationOutcomeFactory
    {
        internal static VerificationOutcomePayload Create(
            bool buildStarted,
            bool buildSucceeded,
            string? failedStage,
            string? failureReasonCode,
            string fallbackReasonCode)
        {
            return new VerificationOutcomePayload
            {
                Status = BuildStatus(buildStarted, buildSucceeded).ToString(),
                BuildStarted = buildStarted,
                BuildSucceeded = buildSucceeded,
                FailedStage = failedStage ?? string.Empty,
                ReasonCode = failureReasonCode ?? fallbackReasonCode
            };
        }

        private static VerificationOutcomeStatus BuildStatus(bool buildStarted, bool buildSucceeded)
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
