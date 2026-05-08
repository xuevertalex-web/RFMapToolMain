using LocalCursorAgent.Security;

using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    internal static class ActionOutcomeCountersBuilder
    {
        internal static ActionOutcomeCounters Build(
            IReadOnlyList<ActionApprovalProposal> approvalRequiredActions,
            int tracerDeniedActions,
            IReadOnlyList<ActionLifecycleEntry> actionLifecycleEntries)
        {
            return new ActionOutcomeCounters
            {
                ExternalAttempts = approvalRequiredActions.Count,
                OutsideBoundaryAttempts = approvalRequiredActions.Count(x => x is not null && !x.IsInsideSandbox),
                HighRiskApprovalRequiredActions = approvalRequiredActions.Count(x => x is not null && string.Equals(x.RiskLevel, "high", StringComparison.OrdinalIgnoreCase)),
                DeniedActions = tracerDeniedActions,
                RequestedActions = actionLifecycleEntries.Count(e => e.LifecycleState == ActionLifecycleState.Requested),
                BlockedActions = actionLifecycleEntries.Count(e => e.LifecycleState == ActionLifecycleState.Blocked),
                ExecutedActions = actionLifecycleEntries.Count(e => e.LifecycleState == ActionLifecycleState.Executed),
                FailedActions = actionLifecycleEntries.Count(e => e.LifecycleState == ActionLifecycleState.Failed)
            };
        }
    }

    internal sealed class ActionOutcomeCounters
    {
        public int ExternalAttempts { get; init; }
        public int OutsideBoundaryAttempts { get; init; }
        public int HighRiskApprovalRequiredActions { get; init; }
        public int DeniedActions { get; init; }
        public int RequestedActions { get; init; }
        public int BlockedActions { get; init; }
        public int ExecutedActions { get; init; }
        public int FailedActions { get; init; }
    }
}
