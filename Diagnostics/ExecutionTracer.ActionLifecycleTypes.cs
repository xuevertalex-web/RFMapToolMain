namespace LocalCursorAgent.Diagnostics
{
    public sealed class DestructiveTraceRecord
    {
        public string OperationKind { get; init; } = string.Empty;
        public string Step { get; init; } = string.Empty;
        public string OriginalPath { get; init; } = string.Empty;
        public string? TargetPath { get; init; }
        public string? SnapshotPath { get; init; }
        public bool PreviewAccepted { get; init; }
        public bool ApplySucceeded { get; init; }
        public bool ApplyFailed { get; init; }
        public bool RollbackSucceeded { get; init; }
        public bool RollbackFailed { get; init; }
        public bool CommitSucceeded { get; init; }
        public bool CommitFailed { get; init; }
        public string? ReasonCode { get; init; }
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        public int StepOrder { get; init; }
    }

    public sealed class PatchTraceRecord
    {
        public string OperationKind { get; init; } = "Patch";
        public string Step { get; init; } = string.Empty;
        public string TargetPath { get; init; } = string.Empty;
        public string? SnapshotHashBeforeApply { get; init; }
        public bool PreviewAccepted { get; init; }
        public bool ApplySucceeded { get; init; }
        public bool ApplyFailed { get; init; }
        public bool RollbackSucceeded { get; init; }
        public bool RollbackFailed { get; init; }
        public string? ReasonCode { get; init; }
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        public int StepOrder { get; init; }
    }

    public enum ActionLifecycleState
    {
        Requested,
        ApprovalRequired,
        Blocked,
        Executed,
        Failed
    }

    public sealed class ActionLifecycleEntry
    {
        public int Sequence { get; init; }
        public string ActionCorrelationId { get; init; } = string.Empty;
        public string ToolName { get; init; } = string.Empty;
        public string ActionType { get; init; } = string.Empty;
        public string Target { get; init; } = string.Empty;
        public string Command { get; init; } = string.Empty;
        public string NormalizedTarget { get; init; } = string.Empty;
        public ActionLifecycleState LifecycleState { get; init; }
        public string ReasonCode { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public string ApprovalStatus { get; init; } = string.Empty;
        public bool IsInsideSandbox { get; init; }
        public DateTime TimestampUtc { get; init; }
    }
}
