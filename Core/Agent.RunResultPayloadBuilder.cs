using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Security;
using System.Collections.Generic;
using System.Linq;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private AgentRunResultPayload BuildAgentRunResultPayload(
            bool ok,
            string message,
            string summary,
            string planningSummary,
            TaskPlan? taskPlan,
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
            ActionOutcomeCounters actionCounters,
            IReadOnlyList<ExecutionTracer.ModelRetryAttemptDiagnostics> retryAttemptDiagnostics,
            AgentSessionContext? sessionContext,
            int llmRetryCount = 0,
            string? llmErrorType = null)
        {
            return new AgentRunResultPayload
            {
                Ok = ok,
                Message = message,
                Summary = summary,
                PlanningSummary = planningSummary,
                TaskPlan = BuildTaskPlanPayload(taskPlan),
                ChangedFiles = normalizedChangedPayload.Files,
                ChangedHints = normalizedChangedPayload.Hints,
                ChangedRanges = normalizedChangedPayload.Ranges,
                ChangedKinds = normalizedChangedPayload.Kinds,
                Workspace = workspace ?? string.Empty,
                ExecutionMode = sessionContext?.ExecutionWorkspaceKind ?? StructuredActionContract.ExecutionWorkspaceKindActiveWorkspace,
                ExecutionWorkspaceKind = sessionContext?.ExecutionWorkspaceKind ?? StructuredActionContract.ExecutionWorkspaceKindActiveWorkspace,
                ActiveWorkspaceUsed = sessionContext?.ActiveWorkspaceUsed ?? true,
                SandboxRoot = sessionContext?.ExecutionWorkspaceRoot ?? workspace ?? string.Empty,
                WorktreeRoot = sessionContext?.WorktreeRoot ?? workspace ?? string.Empty,
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
                ErrorType = llmErrorType ?? string.Empty,
                RetryCount = Math.Max(0, llmRetryCount),
                RetryDiagnostics = new RetryDiagnosticsPayload
                {
                    Attempts = (retryAttemptDiagnostics ?? Array.Empty<ExecutionTracer.ModelRetryAttemptDiagnostics>())
                        .Select(x => new RetryAttemptPayload
                        {
                            Attempt = Math.Max(0, x.Attempt),
                            Reason = x.Reason ?? string.Empty,
                            DelayMs = Math.Max(0, x.DelayMs),
                            WillRetry = x.WillRetry,
                            FinalAttempt = x.FinalAttempt
                        })
                        .ToArray()
                },
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
                ApprovalStatusSummary = ApprovalStatusSummaryBuilder.Build(actionLifecycleEntries),
                ContextDiagnostics = BuildContextDiagnosticsPayload(_analysisModeDiagnostics),
                ProjectMapDiagnostics = BuildProjectMapDiagnosticsPayload(),
                RetrievalPlanningDiagnostics = BuildRetrievalPlanningDiagnosticsPayload(),
                IndexingDiagnostics = BuildIndexingDiagnosticsPayload()
            };
        }

        private ContextDiagnosticsPayload BuildContextDiagnosticsPayload(AnalysisModeDiagnostics analysisModeDiagnostics)
        {
            var diagnostics = Context.ContextBuilder.GetLatestDiagnostics();
            return new ContextDiagnosticsPayload
            {
                Items = diagnostics.Items.Select(x => new ContextDiagnosticsItemPayload
                {
                    Path = x.Path,
                    Reason = x.Reason,
                    Priority = x.Priority,
                    CharCount = x.CharCount
                }).ToArray(),
                TotalFiles = diagnostics.TotalFiles,
                TotalChars = diagnostics.TotalChars,
                BudgetUsed = diagnostics.BudgetUsed,
                BudgetLimit = diagnostics.BudgetLimit,
                DeepAnalysisTask = analysisModeDiagnostics.DeepAnalysisTask,
                DeepAnalysisTrigger = analysisModeDiagnostics.TriggerCategory,
                AnalysisFileBudgetCap = analysisModeDiagnostics.BudgetCapUsed,
                AnalysisContextIncludesFileContents = analysisModeDiagnostics.IncludesFileContents,
                CandidateSeedCategory = _candidateSeedDiagnostics.Category,
                SeededCandidateFiles = (_candidateSeedDiagnostics.Files ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToArray(),
                AuditAnalysisRouting = _auditRoutingDiagnostics.Enabled,
                RoutingOverrideReason = _auditRoutingDiagnostics.Reason,
                BypassedFastPath = _auditRoutingDiagnostics.BypassedFastPath
            };
        }

        private static IndexingDiagnosticsPayload BuildIndexingDiagnosticsPayload()
        {
            var diagnostics = Indexing.ProjectIndexer.GetLatestDiagnostics();
            return new IndexingDiagnosticsPayload
            {
                IndexedFiles = Math.Max(0, diagnostics.IndexedFiles),
                CacheHits = Math.Max(0, diagnostics.CacheHits),
                CacheMisses = Math.Max(0, diagnostics.CacheMisses),
                FullRebuild = diagnostics.FullRebuild,
                PartialRefresh = diagnostics.PartialRefresh
            };
        }

        private static ProjectMapDiagnosticsPayload BuildProjectMapDiagnosticsPayload()
        {
            var diagnostics = Context.ContextBuilder.GetLatestDiagnostics().ProjectMapDiagnostics;
            return new ProjectMapDiagnosticsPayload
            {
                Enabled = diagnostics.Enabled,
                RulesVersion = diagnostics.RulesVersion ?? string.Empty,
                FileCount = Math.Max(0, diagnostics.FileCount),
                ZoneCounts = new Dictionary<string, int>(diagnostics.ZoneCounts, StringComparer.OrdinalIgnoreCase),
                RoleCounts = new Dictionary<string, int>(diagnostics.RoleCounts, StringComparer.OrdinalIgnoreCase),
                EntrypointCount = Math.Max(0, diagnostics.EntrypointCount),
                GeneratedAtUtc = diagnostics.GeneratedAtUtc,
                Warning = diagnostics.Warning ?? string.Empty,
                Error = diagnostics.Error ?? string.Empty
            };
        }

        private static RetrievalPlanningDiagnosticsPayload BuildRetrievalPlanningDiagnosticsPayload()
        {
            var diagnostics = Context.ContextBuilder.GetLatestDiagnostics().RetrievalPlanningDiagnostics;
            return new RetrievalPlanningDiagnosticsPayload
            {
                SelectedZones = (diagnostics.SelectedZones ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                SelectedRoles = (diagnostics.SelectedRoles ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                TopSignalFiles = (diagnostics.TopSignalFiles ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToArray(),
                TopSignalReasons = (diagnostics.TopSignalReasons ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToArray(),
                Reason = diagnostics.Reason ?? string.Empty,
                Confidence = Math.Clamp(diagnostics.Confidence, 0.0, 1.0),
                FallbackUsed = diagnostics.FallbackUsed
            };
        }

        private static TaskPlanPayload? BuildTaskPlanPayload(TaskPlan? taskPlan)
        {
            if (taskPlan is null)
                return null;

            return new TaskPlanPayload
            {
                Mode = taskPlan.Mode.ToString().ToLowerInvariant(),
                Steps = taskPlan.Steps.ToArray(),
                TargetZones = taskPlan.TargetZones.ToArray(),
                TargetRoles = taskPlan.TargetRoles.ToArray(),
                CandidateFiles = taskPlan.CandidateFiles.ToArray(),
                Risks = taskPlan.Risks.ToArray(),
                Checks = taskPlan.Checks.ToArray(),
                StopConditions = taskPlan.StopConditions.ToArray(),
                Confidence = Math.Clamp(taskPlan.Confidence, 0.0, 1.0),
                Reason = taskPlan.Reason ?? string.Empty
            };
        }

    }
}
