namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class AgentRunState
        {
            public string CurrentResponse { get; set; } = string.Empty;
            public string? LastBuildErrorSignature { get; set; }
            public string? LastBuildFailureCode { get; set; }
            public int? LastBuildExitCode { get; set; }
            public bool? LastBuildTimedOut { get; set; }
            public bool? LastBuildErrorMessageTruncated { get; set; }
            public int? LastBuildErrorMessageLength { get; set; }
            public string? LastDeniedToolResult { get; set; }
            public int ActualIterationsUsed { get; set; }
            public string LastSuccessfulStep { get; set; } = "Indexing";
            public string LastKnownAction { get; set; } = "Indexing completed";
            public bool ModelCallStarted { get; set; }
            public bool PatchStarted { get; set; }
            public bool BuildStarted { get; set; }
        }
    }
}
