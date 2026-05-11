using LocalCursorAgent.Context;
using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Security;
using System.Text.Json;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private static string EmitAgentRunResult(
            bool ok,
            string message,
            string summary,
            string reasonCode,
            IEnumerable<string> changedFiles,
            IEnumerable<ChangedHint> changedHints,
            IEnumerable<ChangedRange> changedRanges,
            IEnumerable<ChangedKind> changedKinds,
            bool buildSucceeded,
            bool buildStarted,
            FailurePayload? failure,
            DateTime? runStartedUtc,
            string? workspace,
            string? provider,
            string? model,
            IEnumerable<string>? degradedFlags,
            string? fallbackReason,
            string? fallbackMode,
            string? finalStatus,
            TimelinePayload[]? timeline,
            IReadOnlyList<ActionApprovalProposal> approvalRequiredActions,
            int tracerDeniedActions,
            IReadOnlyList<ActionLifecycleEntry> actionLifecycleEntries,
            IReadOnlyList<ExecutionTracer.ModelRetryAttemptDiagnostics> retryAttemptDiagnostics,
            AgentSessionContext? sessionContext,
            int llmRetryCount = 0,
            string? llmErrorType = null)
        {
            var effectiveReasonCode = EffectiveReasonCodeResolver.Resolve(failure?.ReasonCode, reasonCode);
            var continuation = ContinuationPayloadBuilder.Build(effectiveReasonCode, failure);
            var runtimeTuning = RuntimeTuningPayloadBuilder.Build(provider, model);
            var actionCounters = ActionOutcomeCountersBuilder.Build(approvalRequiredActions, tracerDeniedActions, actionLifecycleEntries);
            var normalizedChangedPayload = NormalizeChangedPayload(
                changedFiles,
                changedHints,
                changedRanges,
                changedKinds);
            var contextDiagnostics = ContextBuilder.GetLatestDiagnostics();
            var taskPlan = TaskPlanner.Build(reasonCode, contextDiagnostics);
            var planningSummary = TaskPlanner.BuildPlanningSummary(taskPlan, contextDiagnostics.RetrievalPlanningDiagnostics.FallbackUsed, contextDiagnostics.RetrievalPlanningDiagnostics.Reason);
            var messageWithPlanning = string.IsNullOrWhiteSpace(planningSummary)
                ? message
                : $"{planningSummary}{Environment.NewLine}{Environment.NewLine}{message}";

            var payload = BuildAgentRunResultPayload(
                ok,
                messageWithPlanning,
                summary,
                planningSummary,
                taskPlan,
                reasonCode,
                normalizedChangedPayload,
                buildSucceeded,
                buildStarted,
                failure,
                runStartedUtc,
                workspace,
                provider,
                model,
                degradedFlags,
                fallbackReason,
                fallbackMode,
                finalStatus,
                timeline,
                approvalRequiredActions,
                actionLifecycleEntries,
                continuation,
                runtimeTuning,
                actionCounters,
                retryAttemptDiagnostics,
                sessionContext,
                llmRetryCount,
                llmErrorType);

            Console.WriteLine(JsonSerializer.Serialize(payload));
            return messageWithPlanning;
        }
    }
}
