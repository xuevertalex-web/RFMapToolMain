using System.Collections.Generic;

namespace LocalCursorAgent.Diagnostics;

public partial class ExecutionTracer
{
    public void LogDestructiveOperation(DestructiveTraceRecord record)
    {
        LogEvent("DestructiveOperation", "Destructive lifecycle step", new Dictionary<string, object>
        {
            { "OperationKind", record.OperationKind },
            { "Step", record.Step },
            { "OriginalPath", record.OriginalPath },
            { "TargetPath", record.TargetPath ?? string.Empty },
            { "SnapshotPath", record.SnapshotPath ?? string.Empty },
            { "PreviewAccepted", record.PreviewAccepted },
            { "ApplySucceeded", record.ApplySucceeded },
            { "ApplyFailed", record.ApplyFailed },
            { "RollbackSucceeded", record.RollbackSucceeded },
            { "RollbackFailed", record.RollbackFailed },
            { "CommitSucceeded", record.CommitSucceeded },
            { "CommitFailed", record.CommitFailed },
            { "ReasonCode", record.ReasonCode ?? string.Empty },
            { "TimestampUtc", record.TimestampUtc.ToString("O") },
            { "StepOrder", record.StepOrder }
        });
    }

    public void LogPatchLifecycle(PatchTraceRecord record)
    {
        LogEvent("PatchLifecycle", "Patch lifecycle step", new Dictionary<string, object>
        {
            { "OperationKind", record.OperationKind },
            { "Step", record.Step },
            { "TargetPath", record.TargetPath },
            { "SnapshotHashBeforeApply", record.SnapshotHashBeforeApply ?? string.Empty },
            { "PreviewAccepted", record.PreviewAccepted },
            { "ApplySucceeded", record.ApplySucceeded },
            { "ApplyFailed", record.ApplyFailed },
            { "RollbackSucceeded", record.RollbackSucceeded },
            { "RollbackFailed", record.RollbackFailed },
            { "ReasonCode", record.ReasonCode ?? string.Empty },
            { "TimestampUtc", record.TimestampUtc.ToString("O") },
            { "StepOrder", record.StepOrder }
        });
    }
}
