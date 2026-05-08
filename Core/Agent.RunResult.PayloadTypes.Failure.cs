using System.Text.Json.Serialization;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        internal sealed class FailurePayload
        {
            public string RootCauseCode { get; init; } = string.Empty;
            public string FailedStage { get; init; } = string.Empty;
            public string LastSuccessfulStep { get; init; } = string.Empty;
            public string FailedStep { get; init; } = string.Empty;
            public string ReasonCode { get; init; } = string.Empty;
            public string Explanation { get; init; } = string.Empty;
            public string PipelineStoppedReason { get; init; } = string.Empty;
            public string DownstreamNotStarted { get; init; } = string.Empty;
            public string LoopStage { get; init; } = string.Empty;
            public int MaxIterations { get; init; }
            public int IterationsUsed { get; init; }
            public string LastKnownAction { get; init; } = string.Empty;
            public bool ModelCallStarted { get; init; }
            public bool PatchStarted { get; init; }
            public bool BuildStarted { get; init; }
            public string BuildFailureCode { get; init; } = string.Empty;
            public int? BuildExitCode { get; init; }
            public bool? BuildTimedOut { get; init; }
            public bool? BuildErrorMessageTruncated { get; init; }
            public int? BuildErrorMessageLength { get; init; }
            public TimelinePayload[] Timeline { get; init; } = Array.Empty<TimelinePayload>();
        }

        internal sealed class TimelinePayload
        {
            [JsonPropertyName("stage")]
            public string Stage { get; init; } = string.Empty;

            [JsonPropertyName("status")]
            public string Status { get; init; } = string.Empty;

            [JsonPropertyName("message")]
            public string Message { get; init; } = string.Empty;
        }
    }
}
