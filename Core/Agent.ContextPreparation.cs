using LocalCursorAgent.Context;
using System.IO;

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
            if (analysisOnlyTask)
            {
                var seeded = SeedAuditCandidates(task, candidateFiles);
                candidateFiles = seeded.Candidates;
                _candidateSeedDiagnostics = new CandidateSeedDiagnostics(seeded.Category, seeded.SeededFiles.Take(5).ToList());
            }
            else
            {
                _candidateSeedDiagnostics = CandidateSeedDiagnostics.Default;
            }
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

        private (List<string> Candidates, string Category, List<string> SeededFiles) SeedAuditCandidates(string task, List<string> existing)
        {
            var text = (task ?? string.Empty).ToLowerInvariant();
            var root = _sessionContext?.ActiveWorkspaceRoot ?? Directory.GetCurrentDirectory();
            var seeds = new List<string>();
            var category = "none";

            void AddIfExists(string rel)
            {
                var abs = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(abs) && !existing.Contains(rel, StringComparer.OrdinalIgnoreCase) && !seeds.Contains(rel, StringComparer.OrdinalIgnoreCase))
                    seeds.Add(rel);
            }

            if (text.Contains("approval") || text.Contains("token") || text.Contains("destructive") || text.Contains("обойти"))
            {
                category = "approval-destructive";
                AddIfExists("Tools/FileTool.cs");
                AddIfExists("Core/Agent.ToolingOrchestration.PrecheckHelpers.cs");
                AddIfExists("Core/Agent.ToolingOrchestration.cs");
                AddIfExists("Security/PermissionGuard.cs");
                AddIfExists("SafetyTests/Program.cs");
            }
            if (text.Contains("command") || text.Contains("process") || text.Contains("shell"))
            {
                category = category == "none" ? "command-process" : category + "+command-process";
                AddIfExists("Execution/SafeProcessRunner.cs");
                AddIfExists("Security/CommandRiskPolicy.cs");
                AddIfExists("Tools/FileTool.cs");
                AddIfExists("SafetyTests/Program.cs");
            }
            if (text.Contains("workspace") || text.Contains("guard") || text.Contains("boundary") || text.Contains("рабочая область"))
            {
                category = category == "none" ? "workspace-boundary" : category + "+workspace-boundary";
                AddIfExists("vscode-extension/workspaceResolver.js");
                AddIfExists("vscode-extension/workspaceTaskClassifier.js");
                AddIfExists("vscode-extension/panelRunController.js");
                AddIfExists("vscode-extension/commandHandlers.js");
                AddIfExists("Program.WorkspacePolicyLoader.cs");
            }
            if (text.Contains("vsix") || text.Contains("install") || text.Contains("update") || text.Contains("workflow") || text.Contains("stale") || text.Contains("package"))
            {
                category = category == "none" ? "install-update" : category + "+install-update";
                AddIfExists("scripts/devtools/Update-VSCodeExtension.cmd");
                AddIfExists("vscode-extension/package.json");
            }
            if (text.Contains("retrieval") || text.Contains("context") || text.Contains("deep analysis"))
            {
                category = category == "none" ? "retrieval-context" : category + "+retrieval-context";
                AddIfExists("Context/ContextBuilder.cs");
                AddIfExists("Context/RetrievalSignalScorer.cs");
                AddIfExists("Context/ProjectRetrievalPlanner.cs");
                AddIfExists("Core/Agent.ContextPreparation.cs");
                AddIfExists("Core/AnalysisContextFormatter.cs");
                AddIfExists("Core/AnalysisPromptBuilder.cs");
            }

            var combined = existing
                .Concat(seeds)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return (combined, category, seeds);
        }
    }
}
