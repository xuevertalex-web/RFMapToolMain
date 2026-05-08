using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Security;

internal static class DestructiveOperationResultFactory
{
    public static DestructiveOperationResult Denied(
        ExecutionTracer? tracer,
        string reasonCode,
        string message,
        string originalPath,
        string? targetPath = null)
    {
        DestructiveOperationTraceHelpers.Trace(
            tracer,
            "Delete",
            "DestructivePreviewRejected",
            originalPath,
            targetPath,
            snapshotPath: null,
            previewAccepted: false,
            applySucceeded: false,
            applyFailed: false,
            rollbackSucceeded: false,
            rollbackFailed: false,
            commitSucceeded: false,
            reasonCode: reasonCode,
            stepOrder: 1);

        tracer?.LogEvent("DestructiveOperation", "Destructive operation denied", new Dictionary<string, object>
        {
            { "OriginalPath", originalPath },
            { "TargetPath", targetPath ?? string.Empty },
            { "ReasonCode", reasonCode },
            { "Reason", message }
        });

        return new DestructiveOperationResult
        {
            DestructivePreviewRejected = true,
            ReasonCode = reasonCode,
            Message = message,
            OriginalPath = originalPath,
            TargetPath = targetPath ?? string.Empty
        };
    }
}
