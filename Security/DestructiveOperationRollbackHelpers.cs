namespace LocalCursorAgent.Security;

public sealed partial class DestructiveOperationSafetyGate
{
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

                if (DestructivePathHelpers.Exists(target))
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
                        DestructivePathHelpers.CopyDirectory(source, target);
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
}
