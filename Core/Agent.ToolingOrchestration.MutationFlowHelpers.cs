namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private static IterationToolingResult BuildUnknownToolCallRejectedResult(
            string unknownToolError,
            bool patchStarted,
            string? lastDeniedToolResult,
            string? lastBuildErrorSignature,
            string? lastBuildFailureCode,
            int toolCallCount)
        {
            return new IterationToolingResult
            {
                NextResponse = $@"Tool call rejected: {unknownToolError}

Use only the registered tools exactly as listed in the prompt. The only valid tool names are 'file' and 'build'. If the task is analysis-only, respond directly without any tool call.",
                ShouldContinue = true,
                PatchStarted = patchStarted,
                BuildStarted = false,
                LastDeniedToolResult = lastDeniedToolResult,
                LastBuildErrorSignature = lastBuildErrorSignature,
                LastBuildFailureCode = lastBuildFailureCode,
                LastSuccessfulStep = "ToolCallsExecuted",
                LastKnownAction = $"Executed {toolCallCount} tool calls"
            };
        }

        private static IterationToolingResult BuildMutationContinuationResult(
            string nextResponse,
            bool patchStarted,
            bool buildStarted,
            string? lastDeniedToolResult,
            string? lastBuildErrorSignature,
            string? lastBuildFailureCode,
            int? lastBuildExitCode,
            bool? lastBuildTimedOut,
            bool? lastBuildErrorMessageTruncated,
            int? lastBuildErrorMessageLength,
            string lastSuccessfulStep,
            string lastKnownAction)
        {
            return new IterationToolingResult
            {
                NextResponse = nextResponse,
                ShouldContinue = true,
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
