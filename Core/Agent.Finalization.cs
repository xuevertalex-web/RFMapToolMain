using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Security;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private string FinalizeRunResult(
            bool ok,
            string message,
            string summary,
            string reasonCode,
            IEnumerable<string> changedFiles,
            IEnumerable<ChangedHint> changedHints,
            IEnumerable<ChangedRange> changedRanges,
            IEnumerable<ChangedKind> changedKinds,
            bool buildSucceeded,
            string? cancelSource = null,
            bool? buildStarted = null,
            FailurePayload? failure = null,
            DateTime? runStartedUtc = null,
            string? workspace = null,
            string? provider = null,
            string? model = null,
            IEnumerable<string>? degradedFlags = null,
            string? fallbackReason = null,
            string? fallbackMode = null,
            string? payloadFinalStatus = null,
            TimelinePayload[]? timeline = null)
        {
            var tracerFinalStatus = cancelSource != null ? "cancelled" : ok ? "succeeded" : "failed";
            foreach (var file in changedFiles)
            {
                _contextBuilder.Tracer.MarkChangedFile(file);
            }

            _contextBuilder.Tracer.CompleteRun(tracerFinalStatus, summary, reasonCode, buildSucceeded, cancelSource);
            return EmitAgentRunResult(
                ok,
                message,
                summary,
                reasonCode,
                changedFiles,
                changedHints,
                changedRanges,
                changedKinds,
                buildSucceeded,
                buildStarted ?? buildSucceeded,
                failure,
                runStartedUtc,
                workspace,
                provider,
                model,
                degradedFlags,
                fallbackReason,
                fallbackMode,
                payloadFinalStatus,
                timeline,
                _contextBuilder.Tracer.GetApprovalRequiredActions(),
                _contextBuilder.Tracer.GetDeniedPermissionDecisionCount(),
                _contextBuilder.Tracer.GetActionLedger());
        }

        private static string? ExtractRequestedNewFilePath(string task)
        {
            return NewFilePathExtractor.ExtractRequestedNewFilePath(task);
        }

        private string FinalizeStructuredDiagnosticResult(string reasonCode, StructuredDiagnostic diagnostic, IEnumerable<string> changedFiles, IEnumerable<ChangedHint> changedHints, IEnumerable<ChangedRange> changedRanges, IEnumerable<ChangedKind> changedKinds)
        {
            _contextBuilder.Tracer.MarkStopPoint("Agent", reasonCode, diagnostic.WhyDenied, Array.Empty<string>());
            _contextBuilder.Tracer.CompleteRun("failed", "Agent stopped with structured diagnostic", reasonCode, false);
            var message = StructuredDiagnosticMessageBuilder.Build(diagnostic);

            return EmitAgentRunResult(
                false,
                message,
                "Agent stopped with structured diagnostic",
                string.Empty,
                changedFiles,
                changedHints,
                changedRanges,
                changedKinds,
                false,
                false,
                failure: null,
                runStartedUtc: null,
                workspace: null,
                provider: null,
                model: null,
                degradedFlags: null,
                fallbackReason: null,
                fallbackMode: null,
                finalStatus: null,
                timeline: null,
                approvalRequiredActions: Array.Empty<ActionApprovalProposal>(),
                tracerDeniedActions: 0,
                actionLifecycleEntries: Array.Empty<ActionLifecycleEntry>());
        }
    }
}
