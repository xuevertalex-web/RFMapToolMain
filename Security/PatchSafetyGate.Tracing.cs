using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Security;

public sealed partial class PatchSafetyGate
{
    private bool ShouldForceFailure(string targetPath, string step)
    {
        if (_failureHook == null)
            return false;

        return _failureHook(new PatchTraceRecord
        {
            OperationKind = "Patch",
            Step = step,
            TargetPath = targetPath,
            TimestampUtc = DateTime.UtcNow
        });
    }

    private void Trace(string step, string targetPath, string? snapshotHash, bool previewAccepted, bool applySucceeded, bool applyFailed, bool rollbackSucceeded, bool rollbackFailed, string? reasonCode, int stepOrder)
    {
        _tracer?.LogPatchLifecycle(new PatchTraceRecord
        {
            OperationKind = "Patch",
            Step = step,
            TargetPath = targetPath,
            SnapshotHashBeforeApply = snapshotHash,
            PreviewAccepted = previewAccepted,
            ApplySucceeded = applySucceeded,
            ApplyFailed = applyFailed,
            RollbackSucceeded = rollbackSucceeded,
            RollbackFailed = rollbackFailed,
            ReasonCode = reasonCode,
            TimestampUtc = DateTime.UtcNow,
            StepOrder = stepOrder
        });
    }
}
