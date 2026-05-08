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
                return new IterationToolingResult
                {
                    NextResponse = currentResponse,
                    ShouldContinue = false,
                    FinalResult = gateFailureResult,
                    PatchStarted = false,
                    BuildStarted = false,
                    LastDeniedToolResult = lastDeniedToolResult,
                    LastBuildErrorSignature = lastBuildErrorSignature,
                    LastBuildFailureCode = lastBuildFailureCode,
                    LastSuccessfulStep = "ToolCallsParsed",
                    LastKnownAction = $"Parsed {toolCalls.Count} tool calls"
                };
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
                return new IterationToolingResult
                {
                    NextResponse = currentResponse,
                    ShouldContinue = false,
                    FinalResult = toolResultsProcessed.FinalResult,
                    PatchStarted = patchStarted,
                    BuildStarted = false,
                    LastDeniedToolResult = toolResultsProcessed.LastDeniedToolResult,
                    LastBuildErrorSignature = lastBuildErrorSignature,
                    LastBuildFailureCode = lastBuildFailureCode,
                    LastSuccessfulStep = "ToolCallsExecuted",
                    LastKnownAction = $"Executed {toolCalls.Count} tool calls"
                };
            }

            lastDeniedToolResult = toolResultsProcessed.LastDeniedToolResult;
            var unknownToolError = toolResultsProcessed.UnknownToolError;
            if (!string.IsNullOrWhiteSpace(unknownToolError))
            {
                return BuildUnknownToolCallRejectedResult(
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
            if (mutationCall != null)
            {
                var buildVerification = await HandleMutationBuildVerificationAsync(
                    mutationCall,
                    changedFiles,
                    changedHints,
                    changedRanges,
                    changedKinds,
                    lastBuildErrorSignature,
                    lastBuildFailureCode);
                if (buildVerification.BuildStarted)
                {
                    buildStarted = true;
                    lastSuccessfulStep = buildVerification.LastSuccessfulStep;
                    lastKnownAction = buildVerification.LastKnownAction;
                }

                if (buildVerification.FinalResult != null)
                {
                    return new IterationToolingResult
                    {
                        NextResponse = currentResponse,
                        ShouldContinue = false,
                        FinalResult = buildVerification.FinalResult,
                        PatchStarted = patchStarted,
                        BuildStarted = buildStarted,
                        LastDeniedToolResult = lastDeniedToolResult,
                        LastBuildErrorSignature = buildVerification.LastBuildErrorSignature,
                        LastBuildFailureCode = buildVerification.LastBuildFailureCode,
                        LastBuildExitCode = buildVerification.LastBuildExitCode,
                        LastBuildTimedOut = buildVerification.LastBuildTimedOut,
                        LastBuildErrorMessageTruncated = buildVerification.LastBuildErrorMessageTruncated,
                        LastBuildErrorMessageLength = buildVerification.LastBuildErrorMessageLength,
                        LastSuccessfulStep = lastSuccessfulStep,
                        LastKnownAction = lastKnownAction
                    };
                }

                lastBuildErrorSignature = buildVerification.LastBuildErrorSignature;
                lastBuildFailureCode = buildVerification.LastBuildFailureCode;
                lastBuildExitCode = buildVerification.LastBuildExitCode;
                lastBuildTimedOut = buildVerification.LastBuildTimedOut;
                lastBuildErrorMessageTruncated = buildVerification.LastBuildErrorMessageTruncated;
                lastBuildErrorMessageLength = buildVerification.LastBuildErrorMessageLength;
                if (buildVerification.NextResponse != null)
                {
                    return BuildMutationContinuationResult(
                        buildVerification.NextResponse,
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

            nextResponse = BuildPostToolContinuationResponse(
                analysisOnlyTask,
                mutationIntentTask,
                mutationCall,
                changedFiles.Count,
                requestedNewFile,
                currentResponse);
            return new IterationToolingResult
            {
                NextResponse = nextResponse,
                ShouldContinue = false,
                PatchStarted = patchStarted,
                BuildStarted = buildStarted,
                LastDeniedToolResult = lastDeniedToolResult,
                LastBuildErrorSignature = lastBuildErrorSignature,
                LastBuildFailureCode = lastBuildFailureCode,
                LastBuildExitCode = lastBuildExitCode,
                LastBuildTimedOut = lastBuildTimedOut,
                LastBuildErrorMessageTruncated = lastBuildErrorMessageTruncated,
                LastBuildErrorMessageLength = lastBuildErrorMessageLength,
                LastSuccessfulStep = lastSuccessfulStep,
                LastKnownAction = lastKnownAction
            };
        }
    }
}
