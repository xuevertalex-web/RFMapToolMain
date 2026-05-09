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
            var precheckResult = TryHandleToolingPrecheck(
                task,
                analysisOnlyTask,
                requestedNewFile,
                currentResponse,
                targetResolution,
                lastDeniedToolResult,
                lastBuildErrorSignature,
                lastBuildFailureCode,
                tracer);
            if (precheckResult.FinalResult != null)
                return precheckResult.FinalResult;

            var toolCalls = precheckResult.ToolCalls;
            var mutationIntentTask = MutationIntentDetector.IsMutationIntentTask(task) || requestedNewFile != null;
            var mutationCall = precheckResult.MutationCall;

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

            var continuationState = CreateInitialToolingContinuationState(toolCalls.Count, currentResponse);
            var buildStarted = continuationState.BuildStarted;
            string? nextResponse = continuationState.NextResponse;
            int? lastBuildExitCode = continuationState.LastBuildExitCode;
            bool? lastBuildTimedOut = continuationState.LastBuildTimedOut;
            bool? lastBuildErrorMessageTruncated = continuationState.LastBuildErrorMessageTruncated;
            int? lastBuildErrorMessageLength = continuationState.LastBuildErrorMessageLength;
            var lastSuccessfulStep = continuationState.LastSuccessfulStep;
            var lastKnownAction = continuationState.LastKnownAction;
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
