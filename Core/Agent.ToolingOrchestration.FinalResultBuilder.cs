namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private static IterationToolingResult BuildToolResultsFinalResult(
            string currentResponse,
            ToolResultsProcessingResult toolResultsProcessed,
            bool patchStarted,
            string? lastBuildErrorSignature,
            string? lastBuildFailureCode,
            int toolCallCount)
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
                LastKnownAction = $"Executed {toolCallCount} tool calls"
            };
        }
    }
}
