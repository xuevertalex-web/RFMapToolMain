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
            var mutationIntentTask = MutationIntentDetector.IsMutationIntentTask(task) || requestedNewFile != null;
            if (mutationCall != null && !mutationIntentTask)
            {
                tracer.LogActionEvent("MutationToolCallRejected", "Agent", ExecutionTracer.ActionLogLevel.Warning, "blocked", "NON_ACTIONABLE_TASK", new Dictionary<string, object?>
                {
                    { "task", task },
                    { "tool", mutationCall.ToolName },
                    { "reason", "Task is conversational or non-actionable; mutation tool call ignored." }
                });

                var noToolMessage = "Понял. Я на связи и готов помочь. Сформулируй конкретное действие (что создать/изменить/проверить), и я выполню его.";
                return new ToolingPrecheckResult(
                    new IterationToolingResult
                    {
                        NextResponse = currentResponse,
                        ShouldContinue = false,
                        FinalResult = FinalizeRunResult(
                            true,
                            noToolMessage,
                            "Agent completed without tool calls",
                            "SUCCESS_NO_TOOL_CALLS",
                            Array.Empty<string>(),
                            Array.Empty<ChangedHint>(),
                            Array.Empty<ChangedRange>(),
                            Array.Empty<ChangedKind>(),
                            false),
                        PatchStarted = false,
                        BuildStarted = false,
                        LastDeniedToolResult = lastDeniedToolResult,
                        LastBuildErrorSignature = lastBuildErrorSignature,
                        LastBuildFailureCode = lastBuildFailureCode,
                        LastSuccessfulStep = "ToolCallsParsed",
                        LastKnownAction = "Rejected mutation tool call for non-actionable task"
                    },
                    toolCalls,
                    mutationCall);
            }

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
