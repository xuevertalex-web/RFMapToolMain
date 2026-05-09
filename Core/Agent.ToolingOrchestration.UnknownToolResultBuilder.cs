namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private IterationToolingResult BuildUnknownToolResult(
            string unknownToolError,
            bool patchStarted,
            string? lastDeniedToolResult,
            string? lastBuildErrorSignature,
            string? lastBuildFailureCode,
            int toolCallCount)
        {
            return BuildUnknownToolCallRejectedResult(
                unknownToolError,
                patchStarted,
                lastDeniedToolResult,
                lastBuildErrorSignature,
                lastBuildFailureCode,
                toolCallCount);
        }
    }
}