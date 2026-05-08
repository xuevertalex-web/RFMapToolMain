using System.Text.Json.Serialization;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class ChangedHintPayload
        {
            [JsonPropertyName("file")]
            public string File { get; init; } = string.Empty;

            [JsonPropertyName("hint")]
            public string Hint { get; init; } = string.Empty;
        }

        private sealed class ChangedRangePayload
        {
            [JsonPropertyName("file")]
            public string File { get; init; } = string.Empty;

            [JsonPropertyName("startLine")]
            public int StartLine { get; init; }

            [JsonPropertyName("endLine")]
            public int EndLine { get; init; }
        }

        private sealed class ChangedKindPayload
        {
            [JsonPropertyName("file")]
            public string File { get; init; } = string.Empty;

            [JsonPropertyName("kind")]
            public string Kind { get; init; } = string.Empty;
        }
    }
}
