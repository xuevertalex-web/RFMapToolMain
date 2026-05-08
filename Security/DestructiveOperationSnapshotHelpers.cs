namespace LocalCursorAgent.Security;

public sealed partial class DestructiveOperationSafetyGate
{
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
                DestructivePathHelpers.CopyDirectory(path, snapshotPath);
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
}
