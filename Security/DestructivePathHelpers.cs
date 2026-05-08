namespace LocalCursorAgent.Security;

internal static class DestructivePathHelpers
{
    public static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    public static string ResolvePath(string workspaceRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var fullPath = Path.IsPathFullyQualified(path)
            ? path
            : Path.Combine(workspaceRoot, path);

        return Path.GetFullPath(fullPath);
    }

    public static void CopyDirectory(string source, string destination)
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
}
