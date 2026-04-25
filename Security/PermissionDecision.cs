namespace LocalCursorAgent.Security;

public sealed class PermissionDecision
{
    public bool Allowed { get; init; }
    public PermissionReasonCode ReasonCode { get; init; }
    public string ReasonCodeName => ReasonCode.ToString();
    public string ReasonCodeString => ReasonCode switch
    {
        PermissionReasonCode.Allowed => PermissionReasonCodes.Allowed,
        PermissionReasonCode.WorkspaceNotResolved => PermissionReasonCodes.WorkspaceRootNotResolved,
        PermissionReasonCode.PathNormalizationFailed => PermissionReasonCodes.PathNormalizationFailed,
        PermissionReasonCode.PathOutsideWorkspace => PermissionReasonCodes.AccessDeniedOutsideWorkspace,
        PermissionReasonCode.ReadOnlyWriteDenied => PermissionReasonCodes.AccessDeniedByMode,
        PermissionReasonCode.ReadOnlyDeleteDenied => PermissionReasonCodes.AccessDeniedDeleteOperation,
        PermissionReasonCode.WriteModeDeleteDenied => PermissionReasonCodes.AccessDeniedDeleteOperation,
        PermissionReasonCode.ProtectedPathDenied => PermissionReasonCodes.ProtectedPathDenied,
        PermissionReasonCode.ReparsePointDenied => PermissionReasonCodes.ReparsePointDenied,
        PermissionReasonCode.NetworkPathDenied => PermissionReasonCodes.NetworkPathDenied,
        PermissionReasonCode.ExtendedLengthPathDenied => PermissionReasonCodes.ExtendedLengthPathDenied,
        PermissionReasonCode.AlternateDataStreamDenied => PermissionReasonCodes.AlternateDataStreamDenied,
        PermissionReasonCode.InvalidPathSyntaxDenied => PermissionReasonCodes.InvalidPathSyntaxDenied,
        PermissionReasonCode.InvalidWorkingDirectory => PermissionReasonCodes.InvalidWorkingDirectory,
        PermissionReasonCode.ToolDeniedByPolicy => PermissionReasonCodes.ToolDeniedByPolicy,
        _ => PermissionReasonCodes.ToolDeniedByPolicy
    };
    public string Message { get; init; } = string.Empty;
    public string? NormalizedTargetPath { get; init; }
    public string? NormalizedWorkspaceRoot { get; init; }

    public static PermissionDecision Allow(string? target, string? workspace) => new()
    {
        Allowed = true,
        ReasonCode = PermissionReasonCode.Allowed,
        Message = "Allowed",
        NormalizedTargetPath = target,
        NormalizedWorkspaceRoot = workspace
    };

    public static PermissionDecision Deny(PermissionReasonCode code, string message, string? target = null, string? workspace = null) => new()
    {
        Allowed = false,
        ReasonCode = code,
        Message = message,
        NormalizedTargetPath = target,
        NormalizedWorkspaceRoot = workspace
    };
}
