namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class ToolingStateApplyResult
        {
            public required bool ShouldReturn { get; init; }
            public string? FinalResult { get; init; }
            public required bool ShouldContinue { get; init; }
        }

        private ToolingStateApplyResult ApplyToolHandlingToRunState(AgentRunState runState, IterationToolingResult toolHandling)
        {
            runState.CurrentResponse = toolHandling.NextResponse;
            runState.LastDeniedToolResult = toolHandling.LastDeniedToolResult;
            runState.PatchStarted = runState.PatchStarted || toolHandling.PatchStarted;
            if (toolHandling.BuildStarted)
            {
                runState.BuildStarted = true;
            }

            if (!string.IsNullOrWhiteSpace(toolHandling.LastSuccessfulStep))
            {
                runState.LastSuccessfulStep = toolHandling.LastSuccessfulStep!;
            }

            if (!string.IsNullOrWhiteSpace(toolHandling.LastKnownAction))
            {
                runState.LastKnownAction = toolHandling.LastKnownAction!;
            }

            runState.LastBuildErrorSignature = toolHandling.LastBuildErrorSignature;
            runState.LastBuildFailureCode = toolHandling.LastBuildFailureCode;
            runState.LastBuildExitCode = toolHandling.LastBuildExitCode;
            runState.LastBuildTimedOut = toolHandling.LastBuildTimedOut;
            runState.LastBuildErrorMessageTruncated = toolHandling.LastBuildErrorMessageTruncated;
            runState.LastBuildErrorMessageLength = toolHandling.LastBuildErrorMessageLength;

            if (toolHandling.FinalResult != null)
            {
                return new ToolingStateApplyResult
                {
                    ShouldReturn = true,
                    FinalResult = toolHandling.FinalResult,
                    ShouldContinue = false
                };
            }

            return new ToolingStateApplyResult
            {
                ShouldReturn = false,
                ShouldContinue = toolHandling.ShouldContinue
            };
        }
    }
}
