using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LocalCursorAgent.Context;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private static class StructuredActionContract
        {
            public const string ExecutionWorkspaceKindActiveWorkspace = "active-workspace";
            public const string ExecutionWorkspaceKindWorktree = "worktree";
            public const string ApprovalStatusAllowed = "Allowed";
            public const string ApprovalStatusApprovalRequired = "ApprovalRequired";
            public const string ApprovalStatusDenied = "Denied";
            public const string ApprovalStatusNotApplicable = "NotApplicable";
            public const string LifecycleStateRequested = "Requested";
            public const string LifecycleStateApprovalRequired = "ApprovalRequired";
            public const string LifecycleStateBlocked = "Blocked";
            public const string LifecycleStateExecuted = "Executed";
            public const string LifecycleStateFailed = "Failed";
        }

        private sealed class AgentRunResultPayload
        {
            [JsonPropertyName("ok")]
            public bool Ok { get; init; }

            [JsonPropertyName("message")]
            public string Message { get; init; } = string.Empty;

            [JsonPropertyName("summary")]
            public string Summary { get; init; } = string.Empty;

            [JsonPropertyName("planningSummary")]
            public string PlanningSummary { get; init; } = string.Empty;

            [JsonPropertyName("taskPlan")]
            public TaskPlanPayload? TaskPlan { get; init; }

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

            [JsonPropertyName("executionMode")]
            public string ExecutionMode { get; init; } = string.Empty;

            [JsonPropertyName("execution_mode")]
            public string ExecutionModeAlias => ExecutionMode;

            [JsonPropertyName("executionWorkspaceKind")]
            public string ExecutionWorkspaceKind { get; init; } = string.Empty;

            [JsonPropertyName("activeWorkspaceUsed")]
            public bool ActiveWorkspaceUsed { get; init; }

            [JsonPropertyName("sandboxRoot")]
            public string SandboxRoot { get; init; } = string.Empty;

            [JsonPropertyName("worktreeRoot")]
            public string WorktreeRoot { get; init; } = string.Empty;

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

            [JsonPropertyName("errorType")]
            public string ErrorType { get; init; } = string.Empty;

            [JsonPropertyName("retryCount")]
            public int RetryCount { get; init; }

            [JsonPropertyName("retryDiagnostics")]
            public RetryDiagnosticsPayload RetryDiagnostics { get; init; } = new();

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

            [JsonPropertyName("contextDiagnostics")]
            public ContextDiagnosticsPayload ContextDiagnostics { get; init; } = new();

            [JsonPropertyName("projectMapDiagnostics")]
            public ProjectMapDiagnosticsPayload ProjectMapDiagnostics { get; init; } = new();

            [JsonPropertyName("retrievalPlanningDiagnostics")]
            public RetrievalPlanningDiagnosticsPayload RetrievalPlanningDiagnostics { get; init; } = new();

            [JsonPropertyName("indexingDiagnostics")]
            public IndexingDiagnosticsPayload IndexingDiagnostics { get; init; } = new();
        }

        private sealed class ContextDiagnosticsPayload
        {
            [JsonPropertyName("items")]
            public ContextDiagnosticsItemPayload[] Items { get; init; } = Array.Empty<ContextDiagnosticsItemPayload>();

            [JsonPropertyName("totalFiles")]
            public int TotalFiles { get; init; }

            [JsonPropertyName("totalChars")]
            public int TotalChars { get; init; }

            [JsonPropertyName("budgetUsed")]
            public int BudgetUsed { get; init; }

            [JsonPropertyName("budgetLimit")]
            public int BudgetLimit { get; init; }

            [JsonPropertyName("deepAnalysisTask")]
            public bool DeepAnalysisTask { get; init; }

            [JsonPropertyName("deepAnalysisTrigger")]
            public string DeepAnalysisTrigger { get; init; } = string.Empty;

            [JsonPropertyName("analysisFileBudgetCap")]
            public int AnalysisFileBudgetCap { get; init; }

            [JsonPropertyName("analysisContextIncludesFileContents")]
            public bool AnalysisContextIncludesFileContents { get; init; }

            [JsonPropertyName("candidateSeedCategory")]
            public string CandidateSeedCategory { get; init; } = string.Empty;

            [JsonPropertyName("seededCandidateFiles")]
            public string[] SeededCandidateFiles { get; init; } = Array.Empty<string>();

            [JsonPropertyName("auditAnalysisRouting")]
            public bool AuditAnalysisRouting { get; init; }

            [JsonPropertyName("routingOverrideReason")]
            public string RoutingOverrideReason { get; init; } = string.Empty;

            [JsonPropertyName("bypassedFastPath")]
            public bool BypassedFastPath { get; init; }
        }

        private sealed class ContextDiagnosticsItemPayload
        {
            [JsonPropertyName("path")]
            public string Path { get; init; } = string.Empty;

            [JsonPropertyName("reason")]
            public string Reason { get; init; } = string.Empty;

            [JsonPropertyName("priority")]
            public int Priority { get; init; }

            [JsonPropertyName("charCount")]
            public int CharCount { get; init; }
        }

        private sealed class ProjectMapDiagnosticsPayload
        {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; init; }

            [JsonPropertyName("rulesVersion")]
            public string RulesVersion { get; init; } = string.Empty;

            [JsonPropertyName("fileCount")]
            public int FileCount { get; init; }

            [JsonPropertyName("zoneCounts")]
            public Dictionary<string, int> ZoneCounts { get; init; } = new(StringComparer.OrdinalIgnoreCase);

            [JsonPropertyName("roleCounts")]
            public Dictionary<string, int> RoleCounts { get; init; } = new(StringComparer.OrdinalIgnoreCase);

            [JsonPropertyName("entrypointCount")]
            public int EntrypointCount { get; init; }

            [JsonPropertyName("generatedAtUtc")]
            public DateTime? GeneratedAtUtc { get; init; }

            [JsonPropertyName("warning")]
            public string Warning { get; init; } = string.Empty;

            [JsonPropertyName("error")]
            public string Error { get; init; } = string.Empty;
        }

        private sealed class RetrievalPlanningDiagnosticsPayload
        {
            [JsonPropertyName("selectedZones")]
            public string[] SelectedZones { get; init; } = Array.Empty<string>();

            [JsonPropertyName("selectedRoles")]
            public string[] SelectedRoles { get; init; } = Array.Empty<string>();

            [JsonPropertyName("topSignalFiles")]
            public string[] TopSignalFiles { get; init; } = Array.Empty<string>();

            [JsonPropertyName("topSignalReasons")]
            public string[] TopSignalReasons { get; init; } = Array.Empty<string>();

            [JsonPropertyName("reason")]
            public string Reason { get; init; } = string.Empty;

            [JsonPropertyName("confidence")]
            public double Confidence { get; init; }

            [JsonPropertyName("fallbackUsed")]
            public bool FallbackUsed { get; init; }
        }

        private sealed class TaskPlanPayload
        {
            [JsonPropertyName("mode")]
            public string Mode { get; init; } = string.Empty;

            [JsonPropertyName("steps")]
            public string[] Steps { get; init; } = Array.Empty<string>();

            [JsonPropertyName("targetZones")]
            public string[] TargetZones { get; init; } = Array.Empty<string>();

            [JsonPropertyName("targetRoles")]
            public string[] TargetRoles { get; init; } = Array.Empty<string>();

            [JsonPropertyName("candidateFiles")]
            public string[] CandidateFiles { get; init; } = Array.Empty<string>();

            [JsonPropertyName("risks")]
            public string[] Risks { get; init; } = Array.Empty<string>();

            [JsonPropertyName("checks")]
            public string[] Checks { get; init; } = Array.Empty<string>();

            [JsonPropertyName("stopConditions")]
            public string[] StopConditions { get; init; } = Array.Empty<string>();

            [JsonPropertyName("confidence")]
            public double Confidence { get; init; }

            [JsonPropertyName("reason")]
            public string Reason { get; init; } = string.Empty;
        }

        private sealed class RetryDiagnosticsPayload
        {
            [JsonPropertyName("attempts")]
            public RetryAttemptPayload[] Attempts { get; init; } = Array.Empty<RetryAttemptPayload>();
        }

        private sealed class RetryAttemptPayload
        {
            [JsonPropertyName("attempt")]
            public int Attempt { get; init; }

            [JsonPropertyName("reason")]
            public string Reason { get; init; } = string.Empty;

            [JsonPropertyName("delayMs")]
            public int DelayMs { get; init; }

            [JsonPropertyName("willRetry")]
            public bool WillRetry { get; init; }

            [JsonPropertyName("finalAttempt")]
            public bool FinalAttempt { get; init; }
        }

        private sealed class IndexingDiagnosticsPayload
        {
            [JsonPropertyName("indexedFiles")]
            public int IndexedFiles { get; init; }

            [JsonPropertyName("cacheHits")]
            public int CacheHits { get; init; }

            [JsonPropertyName("cacheMisses")]
            public int CacheMisses { get; init; }

            [JsonPropertyName("fullRebuild")]
            public bool FullRebuild { get; init; }

            [JsonPropertyName("partialRefresh")]
            public bool PartialRefresh { get; init; }
        }

    }
}
