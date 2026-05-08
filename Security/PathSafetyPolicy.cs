namespace LocalCursorAgent.Security;

internal static class PathSafetyPolicy
{
    public static bool IsUncPath(string path) =>
        path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("//", StringComparison.OrdinalIgnoreCase);

    public static bool HasExtendedLengthPrefix(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ||
         path.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase));

    public static bool HasAlternateDataStreamSyntax(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (HasExtendedLengthPrefix(path) || IsUncPath(path))
            return false;

        var firstColon = path.IndexOf(':');
        if (firstColon < 0)
            return false;

        if (firstColon == 1 && path.Length >= 3 && IsPathSeparator(path[2]))
            return path.IndexOf(':', 3) >= 0;

        return true;
    }

    public static bool HasRelativeDriveSyntax(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Length >= 2 &&
               char.IsLetter(path[0]) &&
               path[1] == ':' &&
               (path.Length == 2 || !IsPathSeparator(path[2]));
    }

    public static bool ContainsReparsePoint(string path)
    {
        var current = new DirectoryInfo(path);

        if (File.Exists(path))
            current = new DirectoryInfo(Path.GetDirectoryName(path) ?? path);

        while (current != null)
        {
            if (!current.Exists)
            {
                current = current.Parent;
                continue;
            }

            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;

            current = current.Parent;
        }

        return false;
    }

    private static bool IsPathSeparator(char ch) => ch == '\\' || ch == '/';
}
