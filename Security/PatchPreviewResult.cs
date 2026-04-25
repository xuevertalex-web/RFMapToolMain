namespace LocalCursorAgent.Security;

public sealed class PatchPreviewResult
{
    public bool PreviewGenerated { get; init; }
    public bool PreviewRejected { get; init; }
    public bool AnchorFound { get; init; }
    public bool FileUnchangedSinceRead { get; init; }
    public string ReasonCode { get; init; } = PermissionReasonCodes.PatchPreviewRejected;
    public string Message { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public string SnapshotHashBeforeApply { get; init; } = string.Empty;
    public string? ResolvedAnchor { get; init; }
    public string? PreviewText { get; init; }
}

public sealed class PatchApplyResult
{
    public bool ApplySucceeded { get; init; }
    public bool ApplyFailed { get; init; }
    public bool RollbackSucceeded { get; init; }
    public bool RollbackFailed { get; init; }
    public string ReasonCode { get; init; } = PermissionReasonCodes.PatchApplyFailed;
    public string Message { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
}
