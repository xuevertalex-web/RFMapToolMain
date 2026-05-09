using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private async Task<IterationToolingResult> HandleIterationToolingAsync(
            string task,
            bool analysisOnlyTask,
            string? requestedNewFile,
            string currentResponse,
            List<string> resolvedFiles,
            TargetResolutionGateResult targetResolution,
            string? lastDeniedToolResult,
            string? lastBuildErrorSignature,
            string? lastBuildFailureCode,
            HashSet<string> changedFiles,
            Dictionary<string, ChangedHint> changedHints,
            Dictionary<string, ChangedRange> changedRanges,
            Dictionary<string, ChangedKind> changedKinds,
            ExecutionTracer tracer)
        {
            if (!_toolCaller.ContainsToolCalls(currentResponse))
            {
                return HandleNoToolCallIterationResult(
                    task,
                    currentResponse,
                    requestedNewFile,
                    lastDeniedToolResult,
                    lastBuildErrorSignature,
                    lastBuildFailureCode);
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
                    return emptyToolResult;
            }

            var mutationIntentTask = MutationIntentDetector.IsMutationIntentTask(task) || requestedNewFile != null;
            var mutationCall = toolCalls.FirstOrDefault(ToolCallMutationHeuristics.IsMutationLikeToolCall);
            if (mutationCall != null &&
                TryValidateMutationToolCalls(task, toolCalls, mutationCall, targetResolution, tracer, out var gateFailureResult))
            {
                return BuildMutationGateFailureResult(
                    currentResponse,
                    gateFailureResult!,
                    lastDeniedToolResult,
                    lastBuildErrorSignature,
                    lastBuildFailureCode,
                    toolCalls.Count);
            }

            var patchStarted = toolCalls.Any(ToolCallMutationHeuristics.IsMutationLikeToolCall);
            var toolResults = await _toolCaller.ExecuteToolCalls(toolCalls);
            var toolResultsProcessed = await ProcessToolResultsAsync(
                task,
                toolCalls,
                resolvedFiles,
                toolResults,
                mutationCall,
                lastDeniedToolResult,
                changedFiles,
                changedHints,
                changedRanges,
                changedKinds,
                tracer);
            if (toolResultsProcessed.FinalResult != null)
            {
                return BuildToolResultsFinalResult(
                    currentResponse,
                    toolResultsProcessed,
                    patchStarted,
                    lastBuildErrorSignature,
                    lastBuildFailureCode,
                    toolCalls.Count);
            }

            lastDeniedToolResult = toolResultsProcessed.LastDeniedToolResult;
            var unknownToolError = toolResultsProcessed.UnknownToolError;
            if (!string.IsNullOrWhiteSpace(unknownToolError))
            {
                return BuildUnknownToolResult(
                    unknownToolError,
                    patchStarted,
                    lastDeniedToolResult,
                    lastBuildErrorSignature,
                    lastBuildFailureCode,
                    toolCalls.Count);
            }

            var buildStarted = false;
            string? nextResponse = currentResponse;
            int? lastBuildExitCode = null;
            bool? lastBuildTimedOut = null;
            bool? lastBuildErrorMessageTruncated = null;
            int? lastBuildErrorMessageLength = null;
            var lastSuccessfulStep = "ToolCallsExecuted";
            var lastKnownAction = $"Executed {toolCalls.Count} tool calls";
            var mutationContinuationResult = await HandleMutationContinuationFlowAsync(
                mutationCall,
                changedFiles,
                changedHints,
                changedRanges,
                changedKinds,
                currentResponse,
                patchStarted,
                lastDeniedToolResult,
                lastBuildErrorSignature,
                lastBuildFailureCode,
                buildStarted,
                lastBuildExitCode,
                lastBuildTimedOut,
                lastBuildErrorMessageTruncated,
                lastBuildErrorMessageLength,
                lastSuccessfulStep,
                lastKnownAction);
            if (mutationContinuationResult.ShouldReturn)
                return mutationContinuationResult.Result!;

            buildStarted = mutationContinuationResult.BuildStarted;
            lastBuildErrorSignature = mutationContinuationResult.LastBuildErrorSignature;
            lastBuildFailureCode = mutationContinuationResult.LastBuildFailureCode;
            lastBuildExitCode = mutationContinuationResult.LastBuildExitCode;
            lastBuildTimedOut = mutationContinuationResult.LastBuildTimedOut;
            lastBuildErrorMessageTruncated = mutationContinuationResult.LastBuildErrorMessageTruncated;
            lastBuildErrorMessageLength = mutationContinuationResult.LastBuildErrorMessageLength;
            lastSuccessfulStep = mutationContinuationResult.LastSuccessfulStep;
            lastKnownAction = mutationContinuationResult.LastKnownAction;

            nextResponse = BuildPostToolContinuationResponse(
                analysisOnlyTask,
                mutationIntentTask,
                mutationCall,
                changedFiles.Count,
                requestedNewFile,
                currentResponse);
            return BuildToolingContinuationResult(
                nextResponse,
                patchStarted,
                buildStarted,
                lastDeniedToolResult,
                lastBuildErrorSignature,
                lastBuildFailureCode,
                lastBuildExitCode,
                lastBuildTimedOut,
                lastBuildErrorMessageTruncated,
                lastBuildErrorMessageLength,
                lastSuccessfulStep,
                lastKnownAction);
        }
    }
}
