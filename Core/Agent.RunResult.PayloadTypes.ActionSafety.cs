using System.Text.Json.Serialization;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        internal sealed class ApprovalStatusSummaryPayload
        {
            [JsonPropertyName("allowed")]
            public int Allowed { get; init; }

            [JsonPropertyName("approvalRequired")]
            public int ApprovalRequired { get; init; }

            [JsonPropertyName("denied")]
            public int Denied { get; init; }

            [JsonPropertyName("notApplicable")]
            public int NotApplicable { get; init; }
        }

        internal sealed class ApprovalRequiredActionPayload
        {
            [JsonPropertyName("actionType")]
            public string ActionType { get; init; } = string.Empty;

            [JsonPropertyName("command")]
            public string Command { get; init; } = string.Empty;

            [JsonPropertyName("path")]
            public string Path { get; init; } = string.Empty;

            [JsonPropertyName("normalizedTarget")]
            public string NormalizedTarget { get; init; } = string.Empty;

            [JsonPropertyName("sandboxRoot")]
            public string SandboxRoot { get; init; } = string.Empty;

            [JsonPropertyName("projectRoot")]
            public string ProjectRoot { get; init; } = string.Empty;

            [JsonPropertyName("worktreeRoot")]
            public string WorktreeRoot { get; init; } = string.Empty;

            [JsonPropertyName("riskLevel")]
            public string RiskLevel { get; init; } = string.Empty;

            [JsonPropertyName("reasonCode")]
            public string ReasonCode { get; init; } = string.Empty;

            [JsonPropertyName("expectedEffect")]
            public string ExpectedEffect { get; init; } = string.Empty;

            [JsonPropertyName("reason")]
            public string Reason { get; init; } = string.Empty;

            [JsonPropertyName("approvalStatus")]
            public string ApprovalStatus { get; init; } = string.Empty;

            [JsonPropertyName("isInsideSandbox")]
            public bool IsInsideSandbox { get; init; }
        }

        internal sealed class ActionLifecyclePayload
        {
            [JsonPropertyName("sequence")]
            public int Sequence { get; init; }

            [JsonPropertyName("actionType")]
            public string ActionType { get; init; } = string.Empty;

            [JsonPropertyName("actionCorrelationId")]
            public string ActionCorrelationId { get; init; } = string.Empty;

            [JsonPropertyName("target")]
            public string Target { get; init; } = string.Empty;

            [JsonPropertyName("command")]
            public string Command { get; init; } = string.Empty;

            [JsonPropertyName("normalizedTarget")]
            public string NormalizedTarget { get; init; } = string.Empty;

            [JsonPropertyName("lifecycleState")]
            public string LifecycleState { get; init; } = string.Empty;

            [JsonPropertyName("reasonCode")]
            public string ReasonCode { get; init; } = string.Empty;

            [JsonPropertyName("reason")]
            public string Reason { get; init; } = string.Empty;

            [JsonPropertyName("approvalStatus")]
            public string ApprovalStatus { get; init; } = string.Empty;

            [JsonPropertyName("isInsideSandbox")]
            public bool IsInsideSandbox { get; init; }
        }
    }
}
