using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class ToolResultsProcessingResult
        {
            public string? FinalResult { get; init; }
            public string? LastDeniedToolResult { get; init; }
            public string? UnknownToolError { get; init; }
        }

        private async Task<ToolResultsProcessingResult> ProcessToolResultsAsync(
            string task,
            List<ToolCaller.ToolCall> toolCalls,
            List<string> resolvedFiles,
            List<string> toolResults,
            ToolCaller.ToolCall? mutationCall,
            string? lastDeniedToolResult,
            HashSet<string> changedFiles,
            Dictionary<string, ChangedHint> changedHints,
            Dictionary<string, ChangedRange> changedRanges,
            Dictionary<string, ChangedKind> changedKinds,
            ExecutionTracer tracer)
        {
            var unknownToolError = toolResults.FirstOrDefault(result =>
                result.StartsWith("Error: Tool '", StringComparison.OrdinalIgnoreCase));
            var currentDeniedToolResult = lastDeniedToolResult;

            foreach (var result in toolResults)
            {
                _memory.Add("tool_result", result.Length > 100 ? result.Substring(0, 100) + "..." : result);

                if (result.StartsWith("DENIED [", StringComparison.OrdinalIgnoreCase))
                {
                    tracer.LogActionEvent("ToolResult", "Agent", ExecutionTracer.ActionLogLevel.Warning, "denied", metadata: new Dictionary<string, object?>
                    {
                        { "tool_result", result }
                    });
                    if (string.Equals(currentDeniedToolResult, result, StringComparison.Ordinal))
                    {
                        return new ToolResultsProcessingResult
                        {
                            FinalResult = FinalizeStructuredDiagnosticResult(
                                "SAFE_REJECTION",
                                new StructuredDiagnostic
                                {
                                    RootCause = "Repeated safety-gate denial after a fix attempt.",
                                    AttemptedFix = mutationCall?.Input ?? "tool call denied",
                                    WhyDenied = result,
                                    NextSafeAction = "Regenerate a safer single-file write for the same target without changing unrelated files."
                                },
                                changedFiles,
                                changedHints.Values,
                                changedRanges.Values,
                                changedKinds.Values),
                            LastDeniedToolResult = currentDeniedToolResult,
                            UnknownToolError = unknownToolError
                        };
                    }

                    currentDeniedToolResult = result;
                }

                await RecordWriteToolEffectsAsync(task, toolCalls, resolvedFiles, changedFiles, changedHints, changedRanges, changedKinds, tracer);
            }

            return new ToolResultsProcessingResult
            {
                FinalResult = null,
                LastDeniedToolResult = currentDeniedToolResult,
                UnknownToolError = unknownToolError
            };
        }
    }
}
