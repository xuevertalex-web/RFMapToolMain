namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class IterationToolingResult
        {
            public required string NextResponse { get; init; }
            public required bool ShouldContinue { get; init; }
            public string? FinalResult { get; init; }
            public required bool PatchStarted { get; init; }
            public required bool BuildStarted { get; init; }
            public string? LastDeniedToolResult { get; init; }
            public string? LastBuildErrorSignature { get; init; }
            public string? LastBuildFailureCode { get; init; }
            public int? LastBuildExitCode { get; init; }
            public bool? LastBuildTimedOut { get; init; }
            public bool? LastBuildErrorMessageTruncated { get; init; }
            public int? LastBuildErrorMessageLength { get; init; }
            public string? LastSuccessfulStep { get; init; }
            public string? LastKnownAction { get; init; }
        }
    }
}
