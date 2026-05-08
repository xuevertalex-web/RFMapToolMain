namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class MutationBuildVerificationResult
        {
            public required bool BuildStarted { get; init; }
            public required string LastSuccessfulStep { get; init; }
            public required string LastKnownAction { get; init; }
            public string? LastBuildErrorSignature { get; init; }
            public string? LastBuildFailureCode { get; init; }
            public int? LastBuildExitCode { get; init; }
            public bool? LastBuildTimedOut { get; init; }
            public bool? LastBuildErrorMessageTruncated { get; init; }
            public int? LastBuildErrorMessageLength { get; init; }
            public string? NextResponse { get; init; }
            public string? FinalResult { get; init; }
        }
    }
}
