internal static class ProgramPathParsingHelpers
{
    public static string ResolveTargetPath(string workspaceRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return workspaceRoot;

        return Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workspaceRoot, path));
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
}
