using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Security;
using System.Linq;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private static AgentRunResultPayload BuildAgentRunResultPayload(
            bool ok,
            string message,
            string summary,
            string reasonCode,
            NormalizedChangedPayload normalizedChangedPayload,
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
            IReadOnlyList<ActionLifecycleEntry> actionLifecycleEntries,
            ContinuationPayloadValues continuation,
            RuntimeTuningPayloadValues runtimeTuning,
            ActionOutcomeCounters actionCounters)
        {
            return new AgentRunResultPayload
            {
                Ok = ok,
                Message = message,
                Summary = summary,
                ChangedFiles = normalizedChangedPayload.Files,
                ChangedHints = normalizedChangedPayload.Hints,
                ChangedRanges = normalizedChangedPayload.Ranges,
                ChangedKinds = normalizedChangedPayload.Kinds,
                Workspace = workspace ?? string.Empty,
                DurationMs = runStartedUtc.HasValue ? Math.Max(0, (long)(DateTime.UtcNow - runStartedUtc.Value).TotalMilliseconds) : null,
                RuntimeElapsedMs = runStartedUtc.HasValue ? Math.Max(0, (long)(DateTime.UtcNow - runStartedUtc.Value).TotalMilliseconds) : null,
                Provider = provider ?? string.Empty,
                Model = model ?? string.Empty,
                RuntimeProfile = runtimeTuning.RuntimeProfile,
                RuntimeEndpoint = runtimeTuning.RuntimeEndpoint,
                ConfiguredContextWindow = runtimeTuning.ConfiguredContextWindow,
                ConfiguredGpuOffloadOptions = runtimeTuning.ConfiguredGpuOffloadOptions,
                RuntimeTuningProfile = runtimeTuning.RuntimeTuningProfile,
                RuntimeTuningOptions = runtimeTuning.RuntimeTuningOptions,
                RuntimeTuningSource = runtimeTuning.RuntimeTuningSource,
                RuntimeTuningApplied = runtimeTuning.RuntimeTuningApplied,
                RuntimeTuningWarnings = runtimeTuning.RuntimeTuningWarnings,
                GpuUsageMeasured = false,
                DegradedFlags = (degradedFlags ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                FallbackReason = fallbackReason ?? string.Empty,
                FallbackMode = fallbackMode ?? string.Empty,
                FinalStatus = finalStatus ?? string.Empty,
                BuildSucceeded = buildSucceeded,
                BuildStarted = buildStarted,
                Verification = BuildVerificationOutcomePayload(
                    buildStarted,
                    buildSucceeded,
                    failure?.FailedStage,
                    failure?.ReasonCode,
                    reasonCode),
                PlanRequired = continuation.PlanRequired,
                ContinuationHint = continuation.ContinuationHint,
                SessionContinuation = new SessionContinuationPayload
                {
                    LastSuccessfulStep = continuation.LastSuccessfulStep,
                    LastKnownAction = continuation.LastKnownAction
                },
                NextActionCandidates = continuation.NextActionCandidates,
                RootCauseCode = failure?.RootCauseCode ?? (!ok ? reasonCode : string.Empty),
                FailedStage = failure?.FailedStage ?? string.Empty,
                LastSuccessfulStep = failure?.LastSuccessfulStep ?? string.Empty,
                FailedStep = failure?.FailedStep ?? string.Empty,
                ReasonCode = failure?.ReasonCode ?? reasonCode,
                Explanation = failure?.Explanation ?? string.Empty,
                PipelineStoppedReason = failure?.PipelineStoppedReason ?? string.Empty,
                DownstreamNotStarted = failure?.DownstreamNotStarted ?? string.Empty,
                LoopStage = failure?.LoopStage ?? string.Empty,
                MaxIterations = failure?.MaxIterations,
                IterationsUsed = failure?.IterationsUsed,
                LastKnownAction = failure?.LastKnownAction ?? string.Empty,
                ModelCallStarted = failure?.ModelCallStarted,
                PatchStarted = failure?.PatchStarted,
                BuildFailureCode = BuildFailureReasonCodeMapper.ToStructuredReasonCode(failure?.BuildFailureCode ?? string.Empty),
                BuildExitCode = failure?.BuildExitCode,
                BuildTimedOut = failure?.BuildTimedOut,
                BuildErrorMessageTruncated = failure?.BuildErrorMessageTruncated,
                BuildErrorMessageLength = failure?.BuildErrorMessageLength,
                Timeline = timeline ?? failure?.Timeline ?? Array.Empty<TimelinePayload>(),
                ApprovalRequiredActions = ApprovalProposalMapper.MapApprovalProposals(approvalRequiredActions),
                ExternalAttempts = actionCounters.ExternalAttempts,
                OutsideBoundaryAttempts = actionCounters.OutsideBoundaryAttempts,
                HighRiskApprovalRequiredActions = actionCounters.HighRiskApprovalRequiredActions,
                DeniedActions = actionCounters.DeniedActions,
                RequestedActions = actionCounters.RequestedActions,
                BlockedActions = actionCounters.BlockedActions,
                ExecutedActions = actionCounters.ExecutedActions,
                FailedActions = actionCounters.FailedActions,
                HostBoundaryPreserved = true,
                ActionLifecycle = ActionLifecycleMapper.MapActionLifecycle(actionLifecycleEntries),
                ApprovalStatusSummary = ApprovalStatusSummaryBuilder.Build(actionLifecycleEntries)
            };
        }

    }
}
