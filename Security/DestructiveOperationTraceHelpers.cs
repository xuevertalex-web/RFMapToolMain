using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Security;

internal static class DestructiveOperationTraceHelpers
{
    public static void Trace(
        ExecutionTracer? tracer,
        string operationKind,
        string step,
        string originalPath,
        string? targetPath,
        string? snapshotPath,
        bool previewAccepted,
        bool applySucceeded,
        bool applyFailed,
        bool rollbackSucceeded,
        bool rollbackFailed,
        bool commitSucceeded,
        string? reasonCode,
        int stepOrder)
    {
        tracer?.LogDestructiveOperation(new DestructiveTraceRecord
        {
            OperationKind = operationKind,
            Step = step,
            OriginalPath = originalPath,
            TargetPath = targetPath,
            SnapshotPath = snapshotPath,
            PreviewAccepted = previewAccepted,
            ApplySucceeded = applySucceeded,
            ApplyFailed = applyFailed,
            RollbackSucceeded = rollbackSucceeded,
            RollbackFailed = rollbackFailed,
            CommitSucceeded = commitSucceeded,
            CommitFailed = false,
            ReasonCode = reasonCode,
            TimestampUtc = DateTime.UtcNow,
            StepOrder = stepOrder
        });
    }

    public static bool ShouldForceFailure(
        Func<DestructiveTraceRecord, bool>? failureHook,
        string operationKind,
        string step,
        string originalPath,
        string? targetPath)
    {
        if (failureHook == null)
            return false;

        return failureHook(new DestructiveTraceRecord
        {
            OperationKind = operationKind,
            Step = step,
            OriginalPath = originalPath,
            TargetPath = targetPath,
            TimestampUtc = DateTime.UtcNow
        });
    }
}
