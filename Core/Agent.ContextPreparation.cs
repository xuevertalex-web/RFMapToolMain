using LocalCursorAgent.Context;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class IterationContextPreparationResult
        {
            public required bool Success { get; init; }
            public IterationContextPreparation? PreparedContext { get; init; }
            public string? FailureResult { get; init; }
        }

        private sealed class IterationContextPreparation
        {
            public required List<string> CandidateFiles { get; init; }
            public required List<string> ResolvedFiles { get; init; }
            public required ContextInformation ContextInfo { get; init; }
            public required string ContextString { get; init; }
        }

        private async Task<IterationContextPreparationResult> TryPrepareIterationContextAsync(
            string task,
            bool analysisOnlyTask,
            List<string>? gatedTargetFiles,
            TargetResolutionGateResult targetResolution)
        {
            var deepDecision = analysisOnlyTask
                ? AnalysisPromptBuilder.EvaluateDeepAnalysisTask(task)
                : new AnalysisPromptBuilder.DeepAnalysisDecision(false, "none");
            var deepAnalysisTask = analysisOnlyTask && deepDecision.IsDeep;
            var semanticTopK = analysisOnlyTask ? 8 : 25 + CONTEXT_EXPANSION_BUFFER;
            var candidateFiles = gatedTargetFiles ?? await _projectIndexer.FindRelevantFiles(task, semanticTopK);
            if (candidateFiles.Count > 0 && gatedTargetFiles == null)
            {
                _memory.Add("semantic_matches", string.Join(", ", candidateFiles));
            }

            var planningSignals = _contextBuilder.ComputeBudgetPlan(task, candidateFiles, new List<string>());
            if (analysisOnlyTask)
            {
                var analysisFileBudget = deepAnalysisTask
                    ? AnalysisPromptBuilder.DeepAnalysisFileBudget
                    : AnalysisPromptBuilder.NormalAnalysisFileBudget;
                planningSignals.Budget = Math.Min(planningSignals.Budget, analysisFileBudget);
                planningSignals.MaxFiles = Math.Min(planningSignals.MaxFiles, analysisFileBudget);
            }

            _memory.Add("context_plan", $"{planningSignals.Complexity}:{planningSignals.Budget}:{planningSignals.Reason}");

            List<string> resolvedFiles;
            if (gatedTargetFiles != null)
            {
                resolvedFiles = gatedTargetFiles;
            }
            else
            {
                if (targetResolution.IsFailed && !analysisOnlyTask)
                {
                    var safeFailure = targetResolution.Reason;
                    _memory.Add("context_failure", safeFailure, "TargetResolutionSafeFailure");
                    _memory.Add("context_failure_reason", "Target resolution returned safe failure before patch generation", safeFailure);
                    _contextBuilder.Tracer.MarkStopPoint("TargetResolutionGate", "TARGET_RESOLUTION_FAILED", safeFailure, new[] { "ModelRequest", "PatchApply", "BuildVerification" });
                    return new IterationContextPreparationResult
                    {
                        Success = false,
                        FailureResult = FinalizeRunResult(false, safeFailure, "Target resolution failed before patch generation", "TARGET_RESOLUTION_FAILED", Array.Empty<string>(), Array.Empty<ChangedHint>(), Array.Empty<ChangedRange>(), Array.Empty<ChangedKind>(), false)
                    };
                }

                resolvedFiles = candidateFiles;
            }

            var contextInfo = _contextBuilder.BuildContext(task, resolvedFiles, new List<string>(), planningSignals.Budget);
            var contextString = analysisOnlyTask
                ? (deepAnalysisTask
                    ? AnalysisContextFormatter.BuildDeepAnalysisContext(contextInfo)
                    : AnalysisContextFormatter.BuildCompactAnalysisContext(contextInfo))
                : _contextBuilder.FormatContext(contextInfo);

            _analysisModeDiagnostics = analysisOnlyTask
                ? new AnalysisModeDiagnostics(
                    deepAnalysisTask,
                    deepAnalysisTask ? deepDecision.Trigger : "none",
                    deepAnalysisTask ? AnalysisPromptBuilder.DeepAnalysisFileBudget : AnalysisPromptBuilder.NormalAnalysisFileBudget,
                    deepAnalysisTask)
                : AnalysisModeDiagnostics.Default;

            return new IterationContextPreparationResult
            {
                Success = true,
                PreparedContext = new IterationContextPreparation
                {
                    CandidateFiles = candidateFiles,
                    ResolvedFiles = resolvedFiles,
                    ContextInfo = contextInfo,
                    ContextString = contextString
                }
            };
        }
    }
}
