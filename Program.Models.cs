using LocalCursorAgent.Security;

internal sealed record ParsedArgs
{
    public string? WorkspacePath { get; init; }
    public string? WorkspacePolicyPath { get; init; }
    public string? LlmProvider { get; init; }
    public string? OllamaModel { get; init; }
    public int? ParentPid { get; init; }
    public List<string> WorkspaceAllowRoots { get; init; } = new();
    public List<string> WorkspaceDenyRoots { get; init; } = new();
    public AgentAccessMode AccessMode { get; init; }
    public string? Task { get; init; }
    public bool Help { get; init; }
}

internal sealed class WorkspacePolicyFile
{
    public string? WorkspacePath { get; set; }
    public AgentAccessMode? AccessMode { get; set; }
    public List<string>? WorkspaceAllowRoots { get; set; }
    public List<string>? WorkspaceDenyRoots { get; set; }
}

internal sealed class WorkspacePolicyLoadResult
{
    public bool Success { get; init; }
    public WorkspacePolicyFile? Policy { get; init; }
    public string ReasonCode { get; init; } = PermissionReasonCodes.Allowed;
    public string ReasonCodeName { get; init; } = nameof(PermissionReasonCodes.Allowed);
    public string Message { get; init; } = string.Empty;

    public static WorkspacePolicyLoadResult CreateSuccess(WorkspacePolicyFile? policy) => new()
    {
        Success = true,
        Policy = policy,
        ReasonCode = PermissionReasonCodes.Allowed,
        ReasonCodeName = nameof(PermissionReasonCodes.Allowed),
        Message = "Workspace policy loaded"
    };

    public static WorkspacePolicyLoadResult Fail(string reasonCode, string message) => new()
    {
        Success = false,
        ReasonCode = reasonCode,
        ReasonCodeName = reasonCode,
        Message = message
    };
}
