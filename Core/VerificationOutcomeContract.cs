using System.Text.Json.Serialization;

namespace LocalCursorAgent.Core
{
    internal enum VerificationOutcomeStatus
    {
        NotStarted,
        Failed,
        Succeeded
    }

    internal sealed class VerificationOutcomePayload
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("buildStarted")]
        public bool BuildStarted { get; set; }

        [JsonPropertyName("buildSucceeded")]
        public bool BuildSucceeded { get; set; }

        [JsonPropertyName("failedStage")]
        public string FailedStage { get; set; } = string.Empty;

        [JsonPropertyName("reasonCode")]
        public string ReasonCode { get; set; } = string.Empty;
    }
}
