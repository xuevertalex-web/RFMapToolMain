public static class ProgramPathParsingHelpers
{
    public static string ResolveTargetPath(string workspaceRoot, string path)
    {
        return TryResolveTargetPath(workspaceRoot, path, out var resolved) ? resolved : string.Empty;
    }

    public static bool TryResolveTargetPath(string workspaceRoot, string path, out string resolved)
    {
        resolved = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            resolved = workspaceRoot;
            return true;
        }

        if (HasAmbiguousWindowsPathForm(path))
            return false;
        if (HasTrailingDotOrSpaceComponent(path))
            return false;

        var full = Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workspaceRoot, path));
        if (HasTrailingDotOrSpaceComponent(full))
            return false;
        resolved = full;
        return true;
    }

    public static int FindWriteSeparator(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return -1;

        if (payload.Length >= 3 && payload[1] == ':' && (payload[2] == '\\' || payload[2] == '/'))
            return payload.IndexOf(':', 3);

        return payload.IndexOf(':');
    }

    public static int FindPathPairSeparator(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return -1;

        if (payload.Length >= 3 && payload[1] == ':' && (payload[2] == '\\' || payload[2] == '/'))
            return payload.IndexOf(':', 3);

        return payload.IndexOf(':');
    }

    private static bool HasAmbiguousWindowsPathForm(string path)
    {
        var value = path.TrimStart();
        return value.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTrailingDotOrSpaceComponent(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var components = trimmed.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var component in components)
        {
            if (component.Length == 0)
                continue;
            var last = component[component.Length - 1];
            if (last == '.' || last == ' ')
                return true;
        }
        return false;
    }
}
