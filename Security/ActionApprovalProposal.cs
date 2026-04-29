namespace LocalCursorAgent.Security;

public enum ApprovalStatus
{
    Allowed,
    Denied,
    ApprovalRequired,
    NotApplicable
}

public sealed class ActionApprovalProposal
{
    public string ActionType { get; init; } = string.Empty;
    public string? Command { get; init; }
    public string? Path { get; init; }
    public string? NormalizedTarget { get; init; }
    public string SandboxRoot { get; init; } = string.Empty;
    public string ProjectRoot { get; init; } = string.Empty;
    public string WorktreeRoot { get; init; } = string.Empty;
    public bool IsInsideSandbox { get; init; }
    public string RiskLevel { get; init; } = "medium";
    public string ReasonCode { get; init; } = string.Empty;
    public string ExpectedEffect { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool RequiresApproval { get; init; }
    public ApprovalStatus ApprovalStatus { get; init; }
}
