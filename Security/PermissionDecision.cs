namespace LocalCursorAgent.Security;

public sealed class PermissionDecision
{
    public bool Allowed { get; init; }
    public bool RequiresApproval { get; init; }
    public ApprovalStatus ApprovalStatus { get; init; } = ApprovalStatus.NotApplicable;
    public PermissionReasonCode ReasonCode { get; init; }
    public string ReasonCodeName => ReasonCode.ToString();
    public string ReasonCodeString => ToReasonCodeString(ReasonCode);

    public static string ToReasonCodeString(PermissionReasonCode reasonCode) => reasonCode switch
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
        PermissionReasonCode.ProtectedRuntimeDiagnosticsPathDenied => PermissionReasonCodes.ProtectedRuntimeDiagnosticsPathDenied,
        PermissionReasonCode.ApprovalTokenExpired => PermissionReasonCodes.ApprovalTokenExpired,
        PermissionReasonCode.ApprovalStateUnavailable => PermissionReasonCodes.ApprovalStateUnavailable,
        PermissionReasonCode.HighRiskApprovalRequired => PermissionReasonCodes.HighRiskApprovalRequired,
        PermissionReasonCode.ToolDeniedByPolicy => PermissionReasonCodes.ToolDeniedByPolicy,
        _ => PermissionReasonCodes.ToolDeniedByPolicy
    };
    public string Message { get; init; } = string.Empty;
    public string? NormalizedTargetPath { get; init; }
    public string? NormalizedWorkspaceRoot { get; init; }
    public ActionApprovalProposal? ApprovalProposal { get; init; }
    public string? ExpectedApprovalToken { get; init; }

    public static PermissionDecision Allow(string? target, string? workspace) => new()
    {
        Allowed = true,
        RequiresApproval = false,
        ApprovalStatus = ApprovalStatus.Allowed,
        ReasonCode = PermissionReasonCode.Allowed,
        Message = "Allowed",
        NormalizedTargetPath = target,
        NormalizedWorkspaceRoot = workspace,
        ExpectedApprovalToken = null
    };

    public static PermissionDecision Deny(PermissionReasonCode code, string message, string? target = null, string? workspace = null) => new()
    {
        Allowed = false,
        RequiresApproval = false,
        ApprovalStatus = ApprovalStatus.Denied,
        ReasonCode = code,
        Message = message,
        NormalizedTargetPath = target,
        NormalizedWorkspaceRoot = workspace,
        ExpectedApprovalToken = null
    };

    public static PermissionDecision ApprovalRequired(
        PermissionReasonCode code,
        string message,
        ActionApprovalProposal proposal,
        string? target = null,
        string? workspace = null) => new()
    {
        Allowed = false,
        RequiresApproval = true,
        ApprovalStatus = ApprovalStatus.ApprovalRequired,
        ReasonCode = code,
        Message = message,
        NormalizedTargetPath = target,
        NormalizedWorkspaceRoot = workspace,
        ApprovalProposal = proposal,
        ExpectedApprovalToken = string.IsNullOrWhiteSpace(proposal.ProposalId) ? null : $"APPROVED:{proposal.ProposalId}"
    };
}
