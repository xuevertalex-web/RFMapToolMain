namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private static IterationToolingResult BuildMutationGateFailureResult(
            string currentResponse,
            string gateFailureResult,
            string? lastDeniedToolResult,
            string? lastBuildErrorSignature,
            string? lastBuildFailureCode,
            int toolCallCount)
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
                LastKnownAction = $"Parsed {toolCallCount} tool calls"
            };
        }
    }
}