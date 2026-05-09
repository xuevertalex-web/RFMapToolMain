namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private static ToolingContinuationState CreateInitialToolingContinuationState(int toolCallCount, string currentResponse)
        {
            return new ToolingContinuationState(
                BuildStarted: false,
                NextResponse: currentResponse,
                LastBuildExitCode: null,
                LastBuildTimedOut: null,
                LastBuildErrorMessageTruncated: null,
                LastBuildErrorMessageLength: null,
                LastSuccessfulStep: "ToolCallsExecuted",
                LastKnownAction: $"Executed {toolCallCount} tool calls");
        }

        private sealed record ToolingContinuationState(
            bool BuildStarted,
            string? NextResponse,
            int? LastBuildExitCode,
            bool? LastBuildTimedOut,
            bool? LastBuildErrorMessageTruncated,
            int? LastBuildErrorMessageLength,
            string LastSuccessfulStep,
            string LastKnownAction);
    }
}
