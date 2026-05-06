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
            var status = VerificationOutcomeBuilder.BuildStatus(buildStarted, buildSucceeded);
            var resolvedFailedStage = VerificationOutcomeFailedStageResolver.Resolve(failedStage);
            var resolvedReasonCode = VerificationOutcomeReasonCodeResolver.Resolve(failureReasonCode, fallbackReasonCode);

            return VerificationOutcomePayloadBuilder.Build(
                status,
                buildStarted,
                buildSucceeded,
                resolvedFailedStage,
                resolvedReasonCode);
        }
    }
}
