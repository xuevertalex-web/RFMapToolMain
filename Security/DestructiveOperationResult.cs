namespace LocalCursorAgent.Security;

public sealed class DestructiveOperationResult
{
    public bool SnapshotCreated { get; init; }
    public bool DestructivePreviewAccepted { get; init; }
    public bool DestructivePreviewRejected { get; init; }
    public bool ApplySucceeded { get; set; }
    public bool ApplyFailed { get; set; }
    public bool RollbackSucceeded { get; set; }
    public bool RollbackFailed { get; set; }
    public bool DestructiveApplySucceeded { get; init; }
    public bool DestructiveApplyFailed { get; init; }
    public bool DestructiveRollbackSucceeded { get; init; }
    public bool DestructiveRollbackFailed { get; init; }
    public string ReasonCode { get; set; } = PermissionReasonCodes.Allowed;
    public string Message { get; set; } = string.Empty;
    public string OriginalPath { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public string SnapshotPath { get; init; } = string.Empty;
}
