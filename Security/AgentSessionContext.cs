namespace LocalCursorAgent.Security;

public sealed class AgentSessionContext
{
    public required string SessionId { get; init; }
    public required string RuntimeRoot { get; init; }
    public required string ActiveWorkspaceRoot { get; set; }
    public string? ExecutionWorkspaceRoot { get; set; }
    public string? WorktreeRoot { get; set; }
    public string ExecutionWorkspaceKind { get; set; } = "active-workspace";
    public bool ActiveWorkspaceUsed { get; set; } = true;
    public required AgentAccessMode AccessMode { get; set; }
    public required ProtectedPathPolicy ProtectedPathPolicy { get; init; }
}
