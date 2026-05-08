namespace LocalCursorAgent.Security;

public sealed partial class DestructiveOperationSafetyGate
{
    public async Task<DestructiveOperationResult> DeleteAsync(string targetPath)
    {
        var resolvedTarget = DestructivePathHelpers.ResolvePath(_session.ActiveWorkspaceRoot, targetPath);
        if (string.IsNullOrWhiteSpace(resolvedTarget))
            return DestructiveOperationResultFactory.Denied(_tracer, PermissionReasonCodes.PathNormalizationFailed, "Failed to normalize target path.", targetPath);

        if (!File.Exists(resolvedTarget) && !Directory.Exists(resolvedTarget))
            return DestructiveOperationResultFactory.Denied(_tracer, PermissionReasonCodes.TargetFileNotFound, "Target path not found.", resolvedTarget);

        var action = new ToolAction { Kind = ToolActionKind.DeleteFile, TargetPath = resolvedTarget };
        var decision = _permissionGuard.Evaluate(_session, action);
        if (!decision.Allowed)
            return DestructiveOperationResultFactory.Denied(_tracer, decision.ReasonCodeString, decision.Message, resolvedTarget);

        Trace("Delete", "SnapshotCreateStarted", resolvedTarget, null, null, false, false, false, false, false, false, null, 1);
        if (ShouldForceFailure("Delete", "SnapshotCreateStarted", resolvedTarget, null))
        {
            Trace("Delete", "SnapshotCreateFailed", resolvedTarget, null, null, false, false, false, false, false, false, PermissionReasonCodes.DeleteSnapshotCreateFailed, 2);
            return DestructiveOperationResultFactory.Denied(_tracer, PermissionReasonCodes.DeleteSnapshotCreateFailed, "Forced snapshot create failure.", resolvedTarget);
        }
        var snapshot = await CreateSnapshotAsync(resolvedTarget);
        if (!snapshot.Created)
        {
            Trace("Delete", "SnapshotCreateFailed", resolvedTarget, null, null, false, false, false, false, false, false, PermissionReasonCodes.DeleteSnapshotCreateFailed, 2);
            return DestructiveOperationResultFactory.Denied(_tracer, PermissionReasonCodes.DeleteSnapshotCreateFailed, snapshot.Message, resolvedTarget, snapshot.SnapshotPath);
        }

        Trace("Delete", "SnapshotCreated", resolvedTarget, null, snapshot.SnapshotPath, true, false, false, false, false, false, null, 2);
        Trace("Delete", "DestructivePreviewAccepted", resolvedTarget, null, snapshot.SnapshotPath, true, false, false, false, false, false, PermissionReasonCodes.Allowed, 3);

        try
        {
            Trace("Delete", "DestructiveApplyStarted", resolvedTarget, null, snapshot.SnapshotPath, true, false, false, false, false, false, null, 4);
            if (ShouldForceFailure("Delete", "DestructiveApplyStarted", resolvedTarget, null))
                throw new InvalidOperationException("Forced destructive apply failure.");
            if (File.Exists(resolvedTarget))
            {
                File.Delete(resolvedTarget);
            }
            else if (Directory.Exists(resolvedTarget))
            {
                Directory.Delete(resolvedTarget, recursive: true);
            }

            if (DestructivePathHelpers.Exists(resolvedTarget))
            {
                Trace("Delete", "DestructiveApplyFailed", resolvedTarget, null, snapshot.SnapshotPath, true, false, true, false, false, false, PermissionReasonCodes.DestructiveApplyFailed, 5);
                Trace("Delete", "DestructiveRollbackStarted", resolvedTarget, null, snapshot.SnapshotPath, true, false, true, false, false, false, PermissionReasonCodes.DeleteRollbackFailed, 6);
                var rollback = await RollbackAsync(snapshot, PermissionReasonCodes.DeleteRollbackFailed, "Delete");
                Trace("Delete", rollback.RollbackSucceeded ? "DestructiveRollbackSucceeded" : "DestructiveRollbackFailed", resolvedTarget, null, snapshot.SnapshotPath, true, false, true, rollback.RollbackSucceeded, rollback.RollbackFailed, false, rollback.ReasonCode, 7);
                rollback.ApplySucceeded = false;
                rollback.ApplyFailed = true;
                rollback.ReasonCode = PermissionReasonCodes.DestructiveApplyFailed;
                rollback.Message = "Delete did not complete cleanly; rollback attempted.";
                return rollback;
            }

            Trace("Delete", "DestructiveApplySucceeded", resolvedTarget, null, snapshot.SnapshotPath, true, true, false, false, false, false, PermissionReasonCodes.Allowed, 5);
            Trace("Delete", "DestructiveCommitSucceeded", resolvedTarget, null, snapshot.SnapshotPath, true, true, false, false, false, true, PermissionReasonCodes.Allowed, 6);
            await CommitSnapshotAsync(snapshot);
            return new DestructiveOperationResult
            {
                SnapshotCreated = true,
                DestructivePreviewAccepted = true,
                ApplySucceeded = true,
                DestructiveApplySucceeded = true,
                ReasonCode = PermissionReasonCodes.Allowed,
                Message = "Delete succeeded.",
                OriginalPath = resolvedTarget,
                SnapshotPath = snapshot.SnapshotPath
            };
        }
        catch (Exception ex)
        {
            Trace("Delete", "DestructiveApplyFailed", resolvedTarget, null, snapshot.SnapshotPath, true, false, true, false, false, false, PermissionReasonCodes.DestructiveApplyFailed, 5);
            Trace("Delete", "DestructiveRollbackStarted", resolvedTarget, null, snapshot.SnapshotPath, true, false, true, false, false, false, PermissionReasonCodes.DeleteRollbackFailed, 6);
            var rollback = await RollbackAsync(snapshot, PermissionReasonCodes.DestructiveApplyFailed, "Delete");
            Trace("Delete", rollback.RollbackSucceeded ? "DestructiveRollbackSucceeded" : "DestructiveRollbackFailed", resolvedTarget, null, snapshot.SnapshotPath, true, false, true, rollback.RollbackSucceeded, rollback.RollbackFailed, false, rollback.ReasonCode, 7);
            rollback.ApplySucceeded = false;
            rollback.ApplyFailed = true;
            rollback.ReasonCode = rollback.RollbackSucceeded ? PermissionReasonCodes.DestructiveApplyFailed : rollback.ReasonCode;
            rollback.Message = ex.Message;
            return rollback;
        }
    }
}
