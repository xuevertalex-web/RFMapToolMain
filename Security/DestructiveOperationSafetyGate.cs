using System.Security.Cryptography;
using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Security;

public sealed partial class DestructiveOperationSafetyGate
{
    private readonly AgentSessionContext _session;
    private readonly PermissionGuard _permissionGuard;
    private readonly ExecutionTracer? _tracer;
    private readonly string _snapshotRoot;
    private readonly Func<DestructiveTraceRecord, bool>? _failureHook;

    public DestructiveOperationSafetyGate(
        AgentSessionContext session,
        PermissionGuard permissionGuard,
        ExecutionTracer? tracer = null,
        Func<DestructiveTraceRecord, bool>? failureHook = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _permissionGuard = permissionGuard ?? throw new ArgumentNullException(nameof(permissionGuard));
        _tracer = tracer;
        _failureHook = failureHook;
        _snapshotRoot = Path.Combine(_session.RuntimeRoot, "snapshots", _session.SessionId);
        Directory.CreateDirectory(_snapshotRoot);
    }

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

    public async Task<DestructiveOperationResult> RenameAsync(string sourcePath, string destinationPath, bool isMove)
    {
        var resolvedSource = DestructivePathHelpers.ResolvePath(_session.ActiveWorkspaceRoot, sourcePath);
        var resolvedDestination = DestructivePathHelpers.ResolvePath(_session.ActiveWorkspaceRoot, destinationPath);
        if (string.IsNullOrWhiteSpace(resolvedSource) || string.IsNullOrWhiteSpace(resolvedDestination))
            return DestructiveOperationResultFactory.Denied(_tracer, PermissionReasonCodes.PathNormalizationFailed, "Failed to normalize source or destination path.", sourcePath, resolvedDestination);

        if (!DestructivePathHelpers.Exists(resolvedSource))
            return DestructiveOperationResultFactory.Denied(_tracer, PermissionReasonCodes.TargetFileNotFound, "Source path not found.", resolvedSource, resolvedDestination);

        if (DestructivePathHelpers.Exists(resolvedDestination))
            return DestructiveOperationResultFactory.Denied(_tracer, PermissionReasonCodes.TargetPathConflict, "Destination path already exists.", resolvedSource, resolvedDestination);

        var action = new ToolAction
        {
            Kind = isMove ? ToolActionKind.MoveFile : ToolActionKind.RenameFile,
            SourcePath = resolvedSource,
            DestinationPath = resolvedDestination
        };

        var decision = _permissionGuard.Evaluate(_session, action);
        if (!decision.Allowed)
            return DestructiveOperationResultFactory.Denied(_tracer, decision.ReasonCodeString, decision.Message, resolvedSource, resolvedDestination);

        Trace(isMove ? "Move" : "Rename", "SnapshotCreateStarted", resolvedSource, resolvedDestination, null, false, false, false, false, false, false, null, 1);
        if (ShouldForceFailure(isMove ? "Move" : "Rename", "SnapshotCreateStarted", resolvedSource, resolvedDestination))
        {
            Trace(isMove ? "Move" : "Rename", "SnapshotCreateFailed", resolvedSource, resolvedDestination, null, false, false, false, false, false, false, PermissionReasonCodes.DeleteSnapshotCreateFailed, 2);
            return DestructiveOperationResultFactory.Denied(_tracer, PermissionReasonCodes.DeleteSnapshotCreateFailed, "Forced snapshot create failure.", resolvedSource, resolvedDestination);
        }
        var snapshot = await CreateSnapshotAsync(resolvedSource);
        if (!snapshot.Created)
        {
            Trace(isMove ? "Move" : "Rename", "SnapshotCreateFailed", resolvedSource, resolvedDestination, null, false, false, false, false, false, false, PermissionReasonCodes.DeleteSnapshotCreateFailed, 2);
            return DestructiveOperationResultFactory.Denied(_tracer, PermissionReasonCodes.DeleteSnapshotCreateFailed, snapshot.Message, resolvedSource, snapshot.SnapshotPath);
        }

        Trace(isMove ? "Move" : "Rename", "SnapshotCreated", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, false, false, false, false, null, 2);
        Trace(isMove ? "Move" : "Rename", "DestructivePreviewAccepted", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, false, false, false, false, PermissionReasonCodes.Allowed, 3);

        try
        {
            Trace(isMove ? "Move" : "Rename", "DestructiveApplyStarted", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, false, false, false, false, null, 4);
            if (ShouldForceFailure(isMove ? "Move" : "Rename", "DestructiveApplyStarted", resolvedSource, resolvedDestination))
                throw new InvalidOperationException("Forced destructive apply failure.");
            if (File.Exists(resolvedSource))
            {
                var destinationDirectory = Path.GetDirectoryName(resolvedDestination);
                if (destinationDirectory != null && !Directory.Exists(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                File.Move(resolvedSource, resolvedDestination);
            }
            else
            {
                var destinationDirectory = Path.GetDirectoryName(resolvedDestination);
                if (destinationDirectory != null && !Directory.Exists(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                Directory.Move(resolvedSource, resolvedDestination);
            }

            if (!DestructivePathHelpers.Exists(resolvedDestination) || DestructivePathHelpers.Exists(resolvedSource))
            {
                Trace(isMove ? "Move" : "Rename", "DestructiveApplyFailed", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, true, false, false, false, PermissionReasonCodes.DestructiveApplyFailed, 5);
                var rollbackReasonCode = DestructiveOperationReasonCodes.ResolveRollbackReasonCode(isMove);
                Trace(isMove ? "Move" : "Rename", "DestructiveRollbackStarted", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, true, false, false, false, rollbackReasonCode, 6);
                var rollback = await RollbackAsync(snapshot, rollbackReasonCode, isMove ? "Move" : "Rename", restoreTo: resolvedSource, renameDestination: resolvedDestination);
                Trace(isMove ? "Move" : "Rename", rollback.RollbackSucceeded ? "DestructiveRollbackSucceeded" : "DestructiveRollbackFailed", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, true, rollback.RollbackSucceeded, rollback.RollbackFailed, false, rollback.ReasonCode, 7);
                rollback.ApplySucceeded = false;
                rollback.ApplyFailed = true;
                rollback.ReasonCode = rollbackReasonCode;
                rollback.Message = "Rename/move verification failed; rollback attempted.";
                return rollback;
            }

            Trace(isMove ? "Move" : "Rename", "DestructiveApplySucceeded", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, true, false, false, false, false, PermissionReasonCodes.Allowed, 5);
            Trace(isMove ? "Move" : "Rename", "DestructiveCommitSucceeded", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, true, false, false, false, true, PermissionReasonCodes.Allowed, 6);
            await CommitSnapshotAsync(snapshot);
            return new DestructiveOperationResult
            {
                SnapshotCreated = true,
                DestructivePreviewAccepted = true,
                ApplySucceeded = true,
                DestructiveApplySucceeded = true,
                ReasonCode = PermissionReasonCodes.Allowed,
                Message = isMove ? "Move succeeded." : "Rename succeeded.",
                OriginalPath = resolvedSource,
                TargetPath = resolvedDestination,
                SnapshotPath = snapshot.SnapshotPath
            };
        }
        catch (Exception ex)
        {
            Trace(isMove ? "Move" : "Rename", "DestructiveApplyFailed", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, true, false, false, false, PermissionReasonCodes.DestructiveApplyFailed, 5);
            var rollbackReasonCode = DestructiveOperationReasonCodes.ResolveRollbackReasonCode(isMove);
            Trace(isMove ? "Move" : "Rename", "DestructiveRollbackStarted", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, true, false, false, false, rollbackReasonCode, 6);
            var rollback = await RollbackAsync(snapshot, rollbackReasonCode, isMove ? "Move" : "Rename", restoreTo: resolvedSource, renameDestination: resolvedDestination);
            Trace(isMove ? "Move" : "Rename", rollback.RollbackSucceeded ? "DestructiveRollbackSucceeded" : "DestructiveRollbackFailed", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, true, rollback.RollbackSucceeded, rollback.RollbackFailed, false, rollback.ReasonCode, 7);
            rollback.ApplySucceeded = false;
            rollback.ApplyFailed = true;
            rollback.ReasonCode = rollbackReasonCode;
            rollback.Message = ex.Message;
            return rollback;
        }
    }

}
