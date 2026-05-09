namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private static IterationToolingResult BuildToolingContinuationResult(
            string? nextResponse,
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
                NextResponse = nextResponse ?? string.Empty,
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
