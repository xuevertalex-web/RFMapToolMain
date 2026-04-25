using System.Security.Cryptography;
using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Security;

public sealed class DestructiveOperationSafetyGate
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
        var resolvedTarget = ResolvePath(targetPath);
        if (string.IsNullOrWhiteSpace(resolvedTarget))
            return Denied(PermissionReasonCodes.PathNormalizationFailed, "Failed to normalize target path.", targetPath);

        if (!File.Exists(resolvedTarget) && !Directory.Exists(resolvedTarget))
            return Denied(PermissionReasonCodes.TargetFileNotFound, "Target path not found.", resolvedTarget);

        var action = new ToolAction { Kind = ToolActionKind.DeleteFile, TargetPath = resolvedTarget };
        var decision = _permissionGuard.Evaluate(_session, action);
        if (!decision.Allowed)
            return Denied(decision.ReasonCodeString, decision.Message, resolvedTarget);

        Trace("Delete", "SnapshotCreateStarted", resolvedTarget, null, null, false, false, false, false, false, false, null, 1);
        if (ShouldForceFailure("Delete", "SnapshotCreateStarted", resolvedTarget, null))
        {
            Trace("Delete", "SnapshotCreateFailed", resolvedTarget, null, null, false, false, false, false, false, false, PermissionReasonCodes.DeleteSnapshotCreateFailed, 2);
            return Denied(PermissionReasonCodes.DeleteSnapshotCreateFailed, "Forced snapshot create failure.", resolvedTarget);
        }
        var snapshot = await CreateSnapshotAsync(resolvedTarget);
        if (!snapshot.Created)
        {
            Trace("Delete", "SnapshotCreateFailed", resolvedTarget, null, null, false, false, false, false, false, false, PermissionReasonCodes.DeleteSnapshotCreateFailed, 2);
            return Denied(PermissionReasonCodes.DeleteSnapshotCreateFailed, snapshot.Message, resolvedTarget, snapshot.SnapshotPath);
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

            if (Exists(resolvedTarget))
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
        var resolvedSource = ResolvePath(sourcePath);
        var resolvedDestination = ResolvePath(destinationPath);
        if (string.IsNullOrWhiteSpace(resolvedSource) || string.IsNullOrWhiteSpace(resolvedDestination))
            return Denied(PermissionReasonCodes.PathNormalizationFailed, "Failed to normalize source or destination path.", sourcePath, resolvedDestination);

        if (!Exists(resolvedSource))
            return Denied(PermissionReasonCodes.TargetFileNotFound, "Source path not found.", resolvedSource, resolvedDestination);

        if (Exists(resolvedDestination))
            return Denied(PermissionReasonCodes.TargetPathConflict, "Destination path already exists.", resolvedSource, resolvedDestination);

        var action = new ToolAction
        {
            Kind = isMove ? ToolActionKind.MoveFile : ToolActionKind.RenameFile,
            SourcePath = resolvedSource,
            DestinationPath = resolvedDestination
        };

        var decision = _permissionGuard.Evaluate(_session, action);
        if (!decision.Allowed)
            return Denied(decision.ReasonCodeString, decision.Message, resolvedSource, resolvedDestination);

        Trace(isMove ? "Move" : "Rename", "SnapshotCreateStarted", resolvedSource, resolvedDestination, null, false, false, false, false, false, false, null, 1);
        if (ShouldForceFailure(isMove ? "Move" : "Rename", "SnapshotCreateStarted", resolvedSource, resolvedDestination))
        {
            Trace(isMove ? "Move" : "Rename", "SnapshotCreateFailed", resolvedSource, resolvedDestination, null, false, false, false, false, false, false, PermissionReasonCodes.DeleteSnapshotCreateFailed, 2);
            return Denied(PermissionReasonCodes.DeleteSnapshotCreateFailed, "Forced snapshot create failure.", resolvedSource, resolvedDestination);
        }
        var snapshot = await CreateSnapshotAsync(resolvedSource);
        if (!snapshot.Created)
        {
            Trace(isMove ? "Move" : "Rename", "SnapshotCreateFailed", resolvedSource, resolvedDestination, null, false, false, false, false, false, false, PermissionReasonCodes.DeleteSnapshotCreateFailed, 2);
            return Denied(PermissionReasonCodes.DeleteSnapshotCreateFailed, snapshot.Message, resolvedSource, snapshot.SnapshotPath);
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

            if (!Exists(resolvedDestination) || Exists(resolvedSource))
            {
                Trace(isMove ? "Move" : "Rename", "DestructiveApplyFailed", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, true, false, false, false, PermissionReasonCodes.DestructiveApplyFailed, 5);
                Trace(isMove ? "Move" : "Rename", "DestructiveRollbackStarted", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, true, false, false, false, isMove ? PermissionReasonCodes.MoveRollbackFailed : PermissionReasonCodes.RenameRollbackFailed, 6);
                var rollback = await RollbackAsync(snapshot, isMove ? PermissionReasonCodes.MoveRollbackFailed : PermissionReasonCodes.RenameRollbackFailed, isMove ? "Move" : "Rename", restoreTo: resolvedSource, renameDestination: resolvedDestination);
                Trace(isMove ? "Move" : "Rename", rollback.RollbackSucceeded ? "DestructiveRollbackSucceeded" : "DestructiveRollbackFailed", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, true, rollback.RollbackSucceeded, rollback.RollbackFailed, false, rollback.ReasonCode, 7);
                rollback.ApplySucceeded = false;
                rollback.ApplyFailed = true;
                rollback.ReasonCode = isMove ? PermissionReasonCodes.MoveRollbackFailed : PermissionReasonCodes.RenameRollbackFailed;
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
            Trace(isMove ? "Move" : "Rename", "DestructiveRollbackStarted", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, true, false, false, false, isMove ? PermissionReasonCodes.MoveRollbackFailed : PermissionReasonCodes.RenameRollbackFailed, 6);
            var rollback = await RollbackAsync(snapshot, isMove ? PermissionReasonCodes.MoveRollbackFailed : PermissionReasonCodes.RenameRollbackFailed, isMove ? "Move" : "Rename", restoreTo: resolvedSource, renameDestination: resolvedDestination);
            Trace(isMove ? "Move" : "Rename", rollback.RollbackSucceeded ? "DestructiveRollbackSucceeded" : "DestructiveRollbackFailed", resolvedSource, resolvedDestination, snapshot.SnapshotPath, true, false, true, rollback.RollbackSucceeded, rollback.RollbackFailed, false, rollback.ReasonCode, 7);
            rollback.ApplySucceeded = false;
            rollback.ApplyFailed = true;
            rollback.ReasonCode = isMove ? PermissionReasonCodes.MoveRollbackFailed : PermissionReasonCodes.RenameRollbackFailed;
            rollback.Message = ex.Message;
            return rollback;
        }
    }

    private async Task<SnapshotInfo> CreateSnapshotAsync(string path)
    {
        try
        {
            var snapshotPath = Path.Combine(_snapshotRoot, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}");
            if (File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
                File.Copy(path, snapshotPath, overwrite: false);
                return new SnapshotInfo(true, snapshotPath, false, path, string.Empty);
            }

            if (Directory.Exists(path))
            {
                Directory.CreateDirectory(snapshotPath);
                CopyDirectory(path, snapshotPath);
                return new SnapshotInfo(true, snapshotPath, true, path, string.Empty);
            }
        }
        catch (Exception ex)
        {
            return new SnapshotInfo(false, string.Empty, false, path, ex.Message);
        }

        return new SnapshotInfo(false, string.Empty, false, path, "Snapshot source not found");
    }

    private async Task CommitSnapshotAsync(SnapshotInfo snapshot)
    {
        try
        {
            if (File.Exists(snapshot.SnapshotPath))
                File.Delete(snapshot.SnapshotPath);
            else if (Directory.Exists(snapshot.SnapshotPath))
                Directory.Delete(snapshot.SnapshotPath, recursive: true);
        }
        catch
        {
            await Task.CompletedTask;
        }
    }

    private async Task<DestructiveOperationResult> RollbackAsync(SnapshotInfo snapshot, string rollbackReasonCode, string operationKind, string? restoreTo = null, string? renameDestination = null)
    {
        try
        {
            Trace(operationKind, "DestructiveRollbackStarted", snapshot.OriginalPath, renameDestination ?? restoreTo, snapshot.SnapshotPath, true, false, true, false, false, false, rollbackReasonCode, 6);
            if (ShouldForceFailure(operationKind, "DestructiveRollbackStarted", snapshot.OriginalPath, renameDestination ?? restoreTo))
                throw new InvalidOperationException("Forced destructive rollback failure.");

            if (snapshot.IsDirectory)
            {
                var source = snapshot.SnapshotPath;
                var target = restoreTo ?? snapshot.OriginalPath;

                if (Exists(target))
                {
                    if (File.Exists(target))
                        File.Delete(target);
                    else
                        Directory.Delete(target, recursive: true);
                }

                if (Directory.Exists(source))
                {
                    var targetDirectory = restoreTo ?? snapshot.OriginalPath;
                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        Directory.CreateDirectory(target);
                        CopyDirectory(source, target);
                    }
                }
            }
            else if (File.Exists(snapshot.SnapshotPath))
            {
                var target = restoreTo ?? snapshot.OriginalPath;
                var dir = Path.GetDirectoryName(target);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.Copy(snapshot.SnapshotPath, target, overwrite: true);
            }

            Trace(operationKind, "DestructiveRollbackSucceeded", snapshot.OriginalPath, renameDestination ?? restoreTo, snapshot.SnapshotPath, true, false, true, true, false, false, PermissionReasonCodes.Allowed, 7);
            return new DestructiveOperationResult
            {
                SnapshotCreated = true,
                DestructivePreviewAccepted = true,
                DestructiveApplyFailed = true,
                DestructiveRollbackSucceeded = true,
                ApplyFailed = true,
                RollbackSucceeded = true,
                ReasonCode = PermissionReasonCodes.Allowed,
                Message = "Rollback succeeded.",
                OriginalPath = snapshot.OriginalPath,
                TargetPath = restoreTo ?? snapshot.OriginalPath,
                SnapshotPath = snapshot.SnapshotPath
            };
        }
        catch (Exception ex)
        {
            Trace(operationKind, "DestructiveRollbackFailed", snapshot.OriginalPath, renameDestination ?? restoreTo, snapshot.SnapshotPath, true, false, true, false, true, false, rollbackReasonCode, 7);
            return new DestructiveOperationResult
            {
                SnapshotCreated = true,
                DestructivePreviewAccepted = true,
                DestructiveApplyFailed = true,
                DestructiveRollbackFailed = true,
                ApplyFailed = true,
                RollbackFailed = true,
                ReasonCode = rollbackReasonCode,
                Message = ex.Message,
                OriginalPath = snapshot.OriginalPath,
                TargetPath = restoreTo ?? snapshot.OriginalPath,
                SnapshotPath = snapshot.SnapshotPath
            };
        }
        finally
        {
            await Task.CompletedTask;
        }
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var fullPath = Path.IsPathFullyQualified(path)
            ? path
            : Path.Combine(_session.ActiveWorkspaceRoot, path);

        return Path.GetFullPath(fullPath);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destFile = Path.Combine(destination, relative);
            var dir = Path.GetDirectoryName(destFile);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.Copy(file, destFile, overwrite: true);
        }
    }

    private DestructiveOperationResult Denied(string reasonCode, string message, string originalPath, string? targetPath = null)
    {
        Trace("Delete", "DestructivePreviewRejected", originalPath, targetPath, null, false, false, false, false, false, false, reasonCode, 1);
        _tracer?.LogEvent("DestructiveOperation", "Destructive operation denied", new Dictionary<string, object>
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
        _tracer?.LogDestructiveOperation(new DestructiveTraceRecord
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

    private bool ShouldForceFailure(string operationKind, string step, string originalPath, string? targetPath)
    {
        if (_failureHook == null)
            return false;

        return _failureHook(new DestructiveTraceRecord
        {
            OperationKind = operationKind,
            Step = step,
            OriginalPath = originalPath,
            TargetPath = targetPath,
            TimestampUtc = DateTime.UtcNow
        });
    }

    private sealed record SnapshotInfo(bool Created, string SnapshotPath, bool IsDirectory, string OriginalPath, string Message);
}

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
