namespace LocalCursorAgent.Security;

public enum PermissionReasonCode
{
    Allowed = 0,
    WorkspaceNotResolved,
    PathNormalizationFailed,
    PathOutsideWorkspace,
    ReadOnlyWriteDenied,
    ReadOnlyDeleteDenied,
    WriteModeDeleteDenied,
    ProtectedPathDenied,
    ReparsePointDenied,
    NetworkPathDenied,
    ExtendedLengthPathDenied,
    AlternateDataStreamDenied,
    InvalidPathSyntaxDenied,
    InvalidWorkingDirectory,
    ProtectedRuntimeDiagnosticsPathDenied,
    ApprovalTokenExpired,
    ApprovalRunBindingUnavailable,
    ApprovalRunMismatch,
    ApprovalCapabilityMismatch,
    ApprovalCapabilityBindingUnavailable,
    ApprovalStateUnavailable,
    HighRiskApprovalRequired,
    CommandUnsupportedShellSyntax,
    CommandHardBlocked,
    CommandMalformed,
    ToolDeniedByPolicy
}
