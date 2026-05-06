namespace LocalCursorAgent.Core
{
    internal static class ContinuationPayloadBuilder
    {
        internal static ContinuationPayloadValues Build(string effectiveReasonCode, Agent.FailurePayload? failure)
        {
            var planRequired = string.Equals(effectiveReasonCode, "NO_ACTIONABLE_STEPS", StringComparison.OrdinalIgnoreCase);
            var lastKnownAction = failure?.LastKnownAction ?? string.Empty;
            var continuationHint = ContinuationGuidanceBuilder.BuildContinuationHint(planRequired, effectiveReasonCode, lastKnownAction);

            return new ContinuationPayloadValues
            {
                PlanRequired = planRequired,
                ContinuationHint = continuationHint,
                LastSuccessfulStep = failure?.LastSuccessfulStep ?? string.Empty,
                LastKnownAction = lastKnownAction,
                NextActionCandidates = ContinuationGuidanceBuilder.BuildNextActionCandidates(planRequired, effectiveReasonCode, continuationHint, lastKnownAction)
            };
        }
    }

    internal sealed class ContinuationPayloadValues
    {
        public bool PlanRequired { get; init; }
        public string ContinuationHint { get; init; } = string.Empty;
        public string LastSuccessfulStep { get; init; } = string.Empty;
        public string LastKnownAction { get; init; } = string.Empty;
        public string[] NextActionCandidates { get; init; } = Array.Empty<string>();
    }
}
