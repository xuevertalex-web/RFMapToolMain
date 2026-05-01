using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    internal static class PatchDecisionBuilder
    {
        public static ExecutionTracer.PatchDecision BuildPatchDecision(string filePath, string input, List<string> alternativeFiles)
        {
            var scope = input.Length > 220 ? "minimal-slice" : "targeted-write";
            var riskLevel = alternativeFiles.Count > 5 ? "medium" : "low";

            return new ExecutionTracer.PatchDecision
            {
                Timestamp = DateTime.UtcNow,
                TargetFile = filePath,
                TargetMethod = string.Empty,
                Scope = scope,
                Reason = "File tool write command selected for minimal patch application",
                RiskLevel = riskLevel,
                AlternativeFiles = alternativeFiles.Take(5).ToList()
            };
        }
    }
}
