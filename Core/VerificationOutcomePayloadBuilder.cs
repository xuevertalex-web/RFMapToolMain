namespace LocalCursorAgent.Core
{
    internal static class VerificationOutcomePayloadBuilder
    {
        internal static VerificationOutcomePayload Build(
            VerificationOutcomeStatus status,
            bool buildStarted,
            bool buildSucceeded,
            string failedStage,
            string reasonCode)
        {
            return new VerificationOutcomePayload
            {
                Status = VerificationOutcomeStatusMapper.ToWireValue(status),
                BuildStarted = buildStarted,
                BuildSucceeded = buildSucceeded,
                FailedStage = failedStage,
                ReasonCode = reasonCode
            };
        }
    }
}
