namespace LocalCursorAgent.Security;

public sealed partial class DestructiveOperationSafetyGate
{
    private void Trace(
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
        DestructiveOperationTraceHelpers.Trace(
            _tracer,
            operationKind,
            step,
            originalPath,
            targetPath,
            snapshotPath,
            previewAccepted,
            applySucceeded,
            applyFailed,
            rollbackSucceeded,
            rollbackFailed,
            commitSucceeded,
            reasonCode,
            stepOrder);
    }

    private bool ShouldForceFailure(string operationKind, string step, string originalPath, string? targetPath)
    {
        return DestructiveOperationTraceHelpers.ShouldForceFailure(_failureHook, operationKind, step, originalPath, targetPath);
    }
}
