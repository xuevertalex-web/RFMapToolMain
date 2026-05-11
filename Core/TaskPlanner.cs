using LocalCursorAgent.Context;
using System.Globalization;

namespace LocalCursorAgent.Core
{
    public static class TaskPlanner
    {
        public static TaskPlan? Build(string reasonCode, ContextDiagnosticsSnapshot diagnostics)
        {
            if (string.Equals(reasonCode, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(reasonCode, "CLARIFICATION_REQUIRED", StringComparison.OrdinalIgnoreCase))
                return null;

            var retrieval = diagnostics.RetrievalPlanningDiagnostics;
            var targetZones = (retrieval.SelectedZones ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var targetRoles = (retrieval.SelectedRoles ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var candidateFiles = (diagnostics.Items ?? new List<ContextDiagnosticsItem>())
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Path)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();

            var mode = InferMode(reasonCode);
            if (mode == TaskPlanMode.Analysis)
                return BuildAnalysisPlan(retrieval, targetZones, targetRoles, candidateFiles);

            return BuildExecutePlan(retrieval, targetZones, targetRoles, candidateFiles);
        }

        public static string BuildPlanningSummary(TaskPlan? taskPlan, bool fallbackUsed, string fallbackReason)
        {
            if (taskPlan is null)
                return string.Empty;

            if (fallbackUsed || taskPlan.TargetZones.Count == 0)
                return "План: не нашёл точной зоны, использую обычный context selection.";

            var zones = string.Join(" + ", taskPlan.TargetZones);
            var roles = taskPlan.TargetRoles.Count > 0 ? $" (роли: {string.Join(", ", taskPlan.TargetRoles)})" : string.Empty;
            var reason = string.IsNullOrWhiteSpace(taskPlan.Reason) ? string.Empty : $". Причина: {taskPlan.Reason}";
            return $"План: посмотрю {zones}{roles}{reason}";
        }

        private static TaskPlanMode InferMode(string reasonCode)
        {
            if (string.Equals(reasonCode, "SUCCESS_ANALYSIS_RESPONSE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(reasonCode, "ANALYSIS_FALLBACK_USED", StringComparison.OrdinalIgnoreCase))
                return TaskPlanMode.Analysis;
            return TaskPlanMode.Execute;
        }

        private static TaskPlan BuildAnalysisPlan(
            RetrievalPlanningDiagnosticsSnapshot retrieval,
            List<string> targetZones,
            List<string> targetRoles,
            List<string> candidateFiles)
        {
            return new TaskPlan
            {
                Mode = TaskPlanMode.Analysis,
                Steps = new List<string> { "inspect", "read", "summarize" },
                TargetZones = targetZones,
                TargetRoles = targetRoles,
                CandidateFiles = candidateFiles,
                Risks = new List<string> { "insufficient_context", "ambiguous_scope" },
                Checks = new List<string> { "no_file_changes" },
                StopConditions = new List<string> { "ambiguous_scope", "missing_target_file" },
                Confidence = Math.Clamp(retrieval.Confidence, 0.0, 1.0),
                Reason = retrieval.Reason ?? string.Empty
            };
        }

        private static TaskPlan BuildExecutePlan(
            RetrievalPlanningDiagnosticsSnapshot retrieval,
            List<string> targetZones,
            List<string> targetRoles,
            List<string> candidateFiles)
        {
            var checks = BuildExecuteChecks(targetZones);
            return new TaskPlan
            {
                Mode = TaskPlanMode.Execute,
                Steps = new List<string> { "inspect", "edit", "test" },
                TargetZones = targetZones,
                TargetRoles = targetRoles,
                CandidateFiles = candidateFiles,
                Risks = new List<string> { "unsafe_path", "regression_risk", "ambiguous_scope" },
                Checks = checks,
                StopConditions = new List<string> { "ambiguous_scope", "missing_target_file", "unsafe_path", "approval_required" },
                Confidence = Math.Clamp(retrieval.Confidence, 0.0, 1.0),
                Reason = retrieval.Reason ?? string.Empty
            };
        }

        private static List<string> BuildExecuteChecks(List<string> targetZones)
        {
            var checks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targetZones.Any(z => z.Equals("vscode-extension", StringComparison.OrdinalIgnoreCase)))
                checks.Add("npm test");

            if (targetZones.Any(z =>
                z.Equals("Core", StringComparison.OrdinalIgnoreCase) ||
                z.Equals("Context", StringComparison.OrdinalIgnoreCase) ||
                z.Equals("Indexing", StringComparison.OrdinalIgnoreCase) ||
                z.Equals("Security", StringComparison.OrdinalIgnoreCase) ||
                z.Equals("Execution", StringComparison.OrdinalIgnoreCase)))
            {
                checks.Add("SmokeGate");
                checks.Add("SafetyTests");
            }

            if (targetZones.Any(z => z.Equals("scripts/devtools", StringComparison.OrdinalIgnoreCase)))
            {
                checks.Add("Doctor-Quick");
                checks.Add("SmokeGate");
            }

            return checks.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
