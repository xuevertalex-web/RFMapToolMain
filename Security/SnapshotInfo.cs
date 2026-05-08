namespace LocalCursorAgent.Security;

internal sealed record SnapshotInfo(bool Created, string SnapshotPath, bool IsDirectory, string OriginalPath, string Message);
