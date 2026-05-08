namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private static VerificationOutcomePayload BuildVerificationOutcomePayload(
            bool buildStarted,
            bool buildSucceeded,
            string? failedStage,
            string? failureReasonCode,
            string fallbackReasonCode)
        {
            var status = !buildStarted
                ? VerificationOutcomeStatus.NotStarted
                : buildSucceeded
                    ? VerificationOutcomeStatus.Succeeded
                    : VerificationOutcomeStatus.Failed;

            return new VerificationOutcomePayload
            {
                Status = status.ToString(),
                BuildStarted = buildStarted,
                BuildSucceeded = buildSucceeded,
                FailedStage = failedStage ?? string.Empty,
                ReasonCode = failureReasonCode ?? fallbackReasonCode
            };
        }
    }
}
