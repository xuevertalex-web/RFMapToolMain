using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private ToolingPrecheckResult TryHandleToolingPrecheck(
            string task,
            bool analysisOnlyTask,
            string? requestedNewFile,
            string currentResponse,
            TargetResolutionGateResult targetResolution,
            string? lastDeniedToolResult,
            string? lastBuildErrorSignature,
            string? lastBuildFailureCode,
            ExecutionTracer tracer)
        {
            if (!_toolCaller.ContainsToolCalls(currentResponse))
            {
                return new ToolingPrecheckResult(
                    HandleNoToolCallIterationResult(
                        task,
                        currentResponse,
                        requestedNewFile,
                        lastDeniedToolResult,
                        lastBuildErrorSignature,
                        lastBuildFailureCode),
                    new List<ToolCaller.ToolCall>(),
                    null);
            }

            var toolCalls = _toolCaller.ParseToolCalls(currentResponse);
            if (toolCalls.Count == 0)
            {
                var emptyToolResult = HandleEmptyToolCallIterationResult(
                    task,
                    analysisOnlyTask,
                    currentResponse,
                    lastDeniedToolResult,
                    lastBuildErrorSignature,
                    lastBuildFailureCode);
                if (emptyToolResult != null)
                    return new ToolingPrecheckResult(emptyToolResult, toolCalls, null);
            }

            var mutationCall = toolCalls.FirstOrDefault(ToolCallMutationHeuristics.IsMutationLikeToolCall);
            if (mutationCall != null &&
                TryValidateMutationToolCalls(task, toolCalls, mutationCall, targetResolution, tracer, out var gateFailureResult))
            {
                return new ToolingPrecheckResult(
                    BuildMutationGateFailureResult(
                        currentResponse,
                        gateFailureResult!,
                        lastDeniedToolResult,
                        lastBuildErrorSignature,
                        lastBuildFailureCode,
                        toolCalls.Count),
                    toolCalls,
                    mutationCall);
            }

            return new ToolingPrecheckResult(null, toolCalls, mutationCall);
        }

        private sealed record ToolingPrecheckResult(
            IterationToolingResult? FinalResult,
            List<ToolCaller.ToolCall> ToolCalls,
            ToolCaller.ToolCall? MutationCall);
    }
}
