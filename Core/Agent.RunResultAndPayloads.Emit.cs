using LocalCursorAgent.Context;
using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Security;
using System.Linq;
using System.Text;
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
            var planningSummary = BuildPlanningSummary(reasonCode);
            var messageWithPlanning = string.IsNullOrWhiteSpace(planningSummary)
                ? message
                : $"{planningSummary}{Environment.NewLine}{Environment.NewLine}{message}";

            var payload = BuildAgentRunResultPayload(
                ok,
                messageWithPlanning,
                summary,
                planningSummary,
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

        private static string BuildPlanningSummary(string reasonCode)
        {
            if (string.Equals(reasonCode, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var diagnostics = ContextBuilder.GetLatestDiagnostics().RetrievalPlanningDiagnostics;
            var zones = diagnostics.SelectedZones
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var roles = diagnostics.SelectedRoles
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (diagnostics.FallbackUsed || zones.Count == 0)
                return "План: не нашёл точной зоны, использую обычный context selection.";

            var sb = new StringBuilder("План: посмотрю ");
            sb.Append(string.Join(" + ", zones));
            if (roles.Count > 0)
            {
                sb.Append(" (роли: ");
                sb.Append(string.Join(", ", roles));
                sb.Append(')');
            }
            if (!string.IsNullOrWhiteSpace(diagnostics.Reason))
            {
                sb.Append(". Причина: ");
                sb.Append(diagnostics.Reason);
            }
            return sb.ToString();
        }
    }
}
