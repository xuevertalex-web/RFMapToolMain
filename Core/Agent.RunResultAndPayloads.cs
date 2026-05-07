using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Security;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            IReadOnlyList<ActionLifecycleEntry> actionLifecycleEntries)
        {
            var effectiveReasonCode = EffectiveReasonCodeResolver.Resolve(failure?.ReasonCode, reasonCode);
            var continuation = ContinuationPayloadBuilder.Build(effectiveReasonCode, failure);
            var runtimeTuning = RuntimeTuningPayloadBuilder.Build(provider, model);
            var actionCounters = ActionOutcomeCountersBuilder.Build(approvalRequiredActions, tracerDeniedActions, actionLifecycleEntries);
            var normalizedChangedArtifacts = ChangedArtifactPayloadBuilder.Normalize(changedFiles, changedHints, changedRanges, changedKinds);
            var normalizedChangedHints = normalizedChangedArtifacts.Hints
                .Select(h => new ChangedHintPayload
                {
                    File = h.File,
                    Hint = h.Hint
                })
                .ToArray();
            var normalizedChangedRanges = normalizedChangedArtifacts.Ranges
                .Select(r => new ChangedRangePayload
                {
                    File = r.File,
                    StartLine = r.StartLine,
                    EndLine = r.EndLine > 0 ? r.EndLine : r.StartLine
                })
                .ToArray();
            var normalizedChangedKinds = normalizedChangedArtifacts.Kinds
                .Select(k => new ChangedKindPayload
                {
                    File = k.File,
                    Kind = k.Kind
                })
                .ToArray();

            var payload = new AgentRunResultPayload
            {
                Ok = ok,
                Message = message,
                Summary = summary,
                ChangedFiles = normalizedChangedArtifacts.Files,
                ChangedHints = normalizedChangedHints,
                ChangedRanges = normalizedChangedRanges,
                ChangedKinds = normalizedChangedKinds,
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
                Verification = VerificationOutcomeFactory.Create(
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

            Console.WriteLine(JsonSerializer.Serialize(payload));
            return message;
        }

        private sealed class AgentRunResultPayload
        {
            [JsonPropertyName("ok")]
            public bool Ok { get; init; }

            [JsonPropertyName("message")]
            public string Message { get; init; } = string.Empty;

            [JsonPropertyName("summary")]
            public string Summary { get; init; } = string.Empty;

            [JsonPropertyName("changedFiles")]
            public string[] ChangedFiles { get; init; } = Array.Empty<string>();

            [JsonPropertyName("changedHints")]
            public ChangedHintPayload[] ChangedHints { get; init; } = Array.Empty<ChangedHintPayload>();

            [JsonPropertyName("changedRanges")]
            public ChangedRangePayload[] ChangedRanges { get; init; } = Array.Empty<ChangedRangePayload>();

            [JsonPropertyName("changedKinds")]
            public ChangedKindPayload[] ChangedKinds { get; init; } = Array.Empty<ChangedKindPayload>();

            [JsonPropertyName("workspace")]
            public string Workspace { get; init; } = string.Empty;

            [JsonPropertyName("durationMs")]
            public long? DurationMs { get; init; }

            [JsonPropertyName("runtimeElapsedMs")]
            public long? RuntimeElapsedMs { get; init; }

            [JsonPropertyName("provider")]
            public string Provider { get; init; } = string.Empty;

            [JsonPropertyName("model")]
            public string Model { get; init; } = string.Empty;

            [JsonPropertyName("runtimeProfile")]
            public string RuntimeProfile { get; init; } = string.Empty;

            [JsonPropertyName("runtimeEndpoint")]
            public string RuntimeEndpoint { get; init; } = string.Empty;

            [JsonPropertyName("configuredContextWindow")]
            public string ConfiguredContextWindow { get; init; } = string.Empty;

            [JsonPropertyName("configuredGpuOffloadOptions")]
            public string ConfiguredGpuOffloadOptions { get; init; } = string.Empty;

            [JsonPropertyName("runtimeTuningProfile")]
            public string RuntimeTuningProfile { get; init; } = string.Empty;

            [JsonPropertyName("runtimeTuningOptions")]
            public string RuntimeTuningOptions { get; init; } = string.Empty;

            [JsonPropertyName("runtimeTuningSource")]
            public string RuntimeTuningSource { get; init; } = string.Empty;

            [JsonPropertyName("runtimeTuningApplied")]
            public bool RuntimeTuningApplied { get; init; }

            [JsonPropertyName("runtimeTuningWarnings")]
            public string[] RuntimeTuningWarnings { get; init; } = Array.Empty<string>();

            [JsonPropertyName("gpuUsageMeasured")]
            public bool GpuUsageMeasured { get; init; }

            [JsonPropertyName("degradedFlags")]
            public string[] DegradedFlags { get; init; } = Array.Empty<string>();

            [JsonPropertyName("fallbackReason")]
            public string FallbackReason { get; init; } = string.Empty;

            [JsonPropertyName("fallbackMode")]
            public string FallbackMode { get; init; } = string.Empty;

            [JsonPropertyName("finalStatus")]
            public string FinalStatus { get; init; } = string.Empty;

            [JsonPropertyName("buildSucceeded")]
            public bool BuildSucceeded { get; init; }

            [JsonPropertyName("buildStarted")]
            public bool BuildStarted { get; init; }

            [JsonPropertyName("verification")]
            public VerificationOutcomePayload Verification { get; init; } = new();

            [JsonPropertyName("planRequired")]
            public bool PlanRequired { get; init; }

            [JsonPropertyName("continuationHint")]
            public string ContinuationHint { get; init; } = string.Empty;

            [JsonPropertyName("sessionContinuation")]
            public SessionContinuationPayload SessionContinuation { get; init; } = new();

            [JsonPropertyName("nextActionCandidates")]
            public string[] NextActionCandidates { get; init; } = Array.Empty<string>();

            [JsonPropertyName("rootCauseCode")]
            public string RootCauseCode { get; init; } = string.Empty;

            [JsonPropertyName("failedStage")]
            public string FailedStage { get; init; } = string.Empty;

            [JsonPropertyName("lastSuccessfulStep")]
            public string LastSuccessfulStep { get; init; } = string.Empty;

            [JsonPropertyName("failedStep")]
            public string FailedStep { get; init; } = string.Empty;

            [JsonPropertyName("reasonCode")]
            public string ReasonCode { get; init; } = string.Empty;

            [JsonPropertyName("explanation")]
            public string Explanation { get; init; } = string.Empty;

            [JsonPropertyName("pipelineStoppedReason")]
            public string PipelineStoppedReason { get; init; } = string.Empty;

            [JsonPropertyName("downstreamNotStarted")]
            public string DownstreamNotStarted { get; init; } = string.Empty;

            [JsonPropertyName("loopStage")]
            public string LoopStage { get; init; } = string.Empty;

            [JsonPropertyName("maxIterations")]
            public int? MaxIterations { get; init; }

            [JsonPropertyName("iterationsUsed")]
            public int? IterationsUsed { get; init; }

            [JsonPropertyName("lastKnownAction")]
            public string LastKnownAction { get; init; } = string.Empty;

            [JsonPropertyName("modelCallStarted")]
            public bool? ModelCallStarted { get; init; }

            [JsonPropertyName("patchStarted")]
            public bool? PatchStarted { get; init; }

            [JsonPropertyName("buildFailureCode")]
            public string BuildFailureCode { get; init; } = string.Empty;

            [JsonPropertyName("buildExitCode")]
            public int? BuildExitCode { get; init; }

            [JsonPropertyName("buildTimedOut")]
            public bool? BuildTimedOut { get; init; }

            [JsonPropertyName("buildErrorMessageTruncated")]
            public bool? BuildErrorMessageTruncated { get; init; }

            [JsonPropertyName("buildErrorMessageLength")]
            public int? BuildErrorMessageLength { get; init; }

            [JsonPropertyName("timeline")]
            public TimelinePayload[] Timeline { get; init; } = Array.Empty<TimelinePayload>();

            [JsonPropertyName("approvalRequiredActions")]
            public ApprovalRequiredActionPayload[] ApprovalRequiredActions { get; init; } = Array.Empty<ApprovalRequiredActionPayload>();

            [JsonPropertyName("externalAttempts")]
            public int ExternalAttempts { get; init; }

            [JsonPropertyName("outsideBoundaryAttempts")]
            public int OutsideBoundaryAttempts { get; init; }

            [JsonPropertyName("highRiskApprovalRequiredActions")]
            public int HighRiskApprovalRequiredActions { get; init; }

            [JsonPropertyName("deniedActions")]
            public int DeniedActions { get; init; }

            [JsonPropertyName("blockedActions")]
            public int BlockedActions { get; init; }

            [JsonPropertyName("requestedActions")]
            public int RequestedActions { get; init; }

            [JsonPropertyName("executedActions")]
            public int ExecutedActions { get; init; }

            [JsonPropertyName("failedActions")]
            public int FailedActions { get; init; }

            [JsonPropertyName("hostBoundaryPreserved")]
            public bool HostBoundaryPreserved { get; init; }

            [JsonPropertyName("actionLifecycle")]
            public ActionLifecyclePayload[] ActionLifecycle { get; init; } = Array.Empty<ActionLifecyclePayload>();

            [JsonPropertyName("approvalStatusSummary")]
            public ApprovalStatusSummaryPayload ApprovalStatusSummary { get; init; } = new();
        }

        internal sealed class ApprovalStatusSummaryPayload
        {
            [JsonPropertyName("allowed")]
            public int Allowed { get; init; }

            [JsonPropertyName("approvalRequired")]
            public int ApprovalRequired { get; init; }

            [JsonPropertyName("denied")]
            public int Denied { get; init; }

            [JsonPropertyName("notApplicable")]
            public int NotApplicable { get; init; }
        }

        private sealed class SessionContinuationPayload
        {
            [JsonPropertyName("lastSuccessfulStep")]
            public string LastSuccessfulStep { get; init; } = string.Empty;

            [JsonPropertyName("lastKnownAction")]
            public string LastKnownAction { get; init; } = string.Empty;
        }

        internal sealed class ApprovalRequiredActionPayload
        {
            [JsonPropertyName("actionType")]
            public string ActionType { get; init; } = string.Empty;

            [JsonPropertyName("command")]
            public string Command { get; init; } = string.Empty;

            [JsonPropertyName("path")]
            public string Path { get; init; } = string.Empty;

            [JsonPropertyName("normalizedTarget")]
            public string NormalizedTarget { get; init; } = string.Empty;

            [JsonPropertyName("sandboxRoot")]
            public string SandboxRoot { get; init; } = string.Empty;

            [JsonPropertyName("projectRoot")]
            public string ProjectRoot { get; init; } = string.Empty;

            [JsonPropertyName("worktreeRoot")]
            public string WorktreeRoot { get; init; } = string.Empty;

            [JsonPropertyName("riskLevel")]
            public string RiskLevel { get; init; } = string.Empty;

            [JsonPropertyName("reasonCode")]
            public string ReasonCode { get; init; } = string.Empty;

            [JsonPropertyName("expectedEffect")]
            public string ExpectedEffect { get; init; } = string.Empty;

            [JsonPropertyName("reason")]
            public string Reason { get; init; } = string.Empty;

            [JsonPropertyName("approvalStatus")]
            public string ApprovalStatus { get; init; } = string.Empty;

            [JsonPropertyName("isInsideSandbox")]
            public bool IsInsideSandbox { get; init; }
        }

        internal sealed class ActionLifecyclePayload
        {
            [JsonPropertyName("sequence")]
            public int Sequence { get; init; }

            [JsonPropertyName("actionType")]
            public string ActionType { get; init; } = string.Empty;

            [JsonPropertyName("actionCorrelationId")]
            public string ActionCorrelationId { get; init; } = string.Empty;

            [JsonPropertyName("target")]
            public string Target { get; init; } = string.Empty;

            [JsonPropertyName("command")]
            public string Command { get; init; } = string.Empty;

            [JsonPropertyName("normalizedTarget")]
            public string NormalizedTarget { get; init; } = string.Empty;

            [JsonPropertyName("lifecycleState")]
            public string LifecycleState { get; init; } = string.Empty;

            [JsonPropertyName("reasonCode")]
            public string ReasonCode { get; init; } = string.Empty;

            [JsonPropertyName("reason")]
            public string Reason { get; init; } = string.Empty;

            [JsonPropertyName("approvalStatus")]
            public string ApprovalStatus { get; init; } = string.Empty;

            [JsonPropertyName("isInsideSandbox")]
            public bool IsInsideSandbox { get; init; }
        }

        internal sealed class FailurePayload
        {
            public string RootCauseCode { get; init; } = string.Empty;
            public string FailedStage { get; init; } = string.Empty;
            public string LastSuccessfulStep { get; init; } = string.Empty;
            public string FailedStep { get; init; } = string.Empty;
            public string ReasonCode { get; init; } = string.Empty;
            public string Explanation { get; init; } = string.Empty;
            public string PipelineStoppedReason { get; init; } = string.Empty;
            public string DownstreamNotStarted { get; init; } = string.Empty;
            public string LoopStage { get; init; } = string.Empty;
            public int MaxIterations { get; init; }
            public int IterationsUsed { get; init; }
            public string LastKnownAction { get; init; } = string.Empty;
            public bool ModelCallStarted { get; init; }
            public bool PatchStarted { get; init; }
            public bool BuildStarted { get; init; }
            public string BuildFailureCode { get; init; } = string.Empty;
            public int? BuildExitCode { get; init; }
            public bool? BuildTimedOut { get; init; }
            public bool? BuildErrorMessageTruncated { get; init; }
            public int? BuildErrorMessageLength { get; init; }
            public TimelinePayload[] Timeline { get; init; } = Array.Empty<TimelinePayload>();
        }

        internal sealed class TimelinePayload
        {
            [JsonPropertyName("stage")]
            public string Stage { get; init; } = string.Empty;

            [JsonPropertyName("status")]
            public string Status { get; init; } = string.Empty;

            [JsonPropertyName("message")]
            public string Message { get; init; } = string.Empty;
        }

        private sealed class ChangedHintPayload
        {
            [JsonPropertyName("file")]
            public string File { get; init; } = string.Empty;

            [JsonPropertyName("hint")]
            public string Hint { get; init; } = string.Empty;
        }

        internal sealed class ChangedHint
        {
            public string File { get; init; } = string.Empty;
            public string Hint { get; init; } = string.Empty;
        }

        internal sealed class ChangedRange
        {
            public string File { get; init; } = string.Empty;
            public int StartLine { get; init; }
            public int EndLine { get; init; }
        }

        private sealed class ChangedRangePayload
        {
            [JsonPropertyName("file")]
            public string File { get; init; } = string.Empty;

            [JsonPropertyName("startLine")]
            public int StartLine { get; init; }

            [JsonPropertyName("endLine")]
            public int EndLine { get; init; }
        }

        internal sealed class ChangedKind
        {
            public string File { get; init; } = string.Empty;
            public string Kind { get; init; } = string.Empty;
        }

        private sealed class ChangedKindPayload
        {
            [JsonPropertyName("file")]
            public string File { get; init; } = string.Empty;

            [JsonPropertyName("kind")]
            public string Kind { get; init; } = string.Empty;
        }

        internal sealed class StructuredDiagnostic
        {
            public string RootCause { get; init; } = string.Empty;
            public string AttemptedFix { get; init; } = string.Empty;
            public string WhyDenied { get; init; } = string.Empty;
            public string NextSafeAction { get; init; } = string.Empty;
        }
    }
}
