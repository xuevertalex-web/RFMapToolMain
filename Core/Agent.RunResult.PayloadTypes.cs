using System.Text.Json.Serialization;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
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

    }
}
