using System.Text.Json.Serialization;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class SessionContinuationPayload
        {
            [JsonPropertyName("lastSuccessfulStep")]
            public string LastSuccessfulStep { get; init; } = string.Empty;

            [JsonPropertyName("lastKnownAction")]
            public string LastKnownAction { get; init; } = string.Empty;
        }
    }
}
