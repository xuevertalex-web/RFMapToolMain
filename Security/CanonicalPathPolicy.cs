namespace LocalCursorAgent.Security;

internal static class CanonicalPathPolicy
{
    public static bool IsCanonicallyContained(string rootPath, string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(candidatePath))
            return false;

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));
        var canonicalRoot = CanonicalizeExistingChain(root);
        var canonicalCandidate = CanonicalizeExistingChain(candidate);

        return canonicalCandidate.Equals(canonicalRoot, StringComparison.OrdinalIgnoreCase) ||
               canonicalCandidate.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string CanonicalizeExistingChain(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(root))
            return Path.TrimEndingDirectorySeparator(full);

        var relative = full[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parts = relative.Length == 0
            ? Array.Empty<string>()
            : relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        var current = Path.TrimEndingDirectorySeparator(root);
        var tail = new List<string>();
        for (var i = 0; i < parts.Length; i++)
        {
            var next = Path.Combine(current, parts[i]);
            if (!Directory.Exists(next) && !File.Exists(next))
            {
                tail.AddRange(parts.Skip(i));
                break;
            }

            current = ResolveLinkIfNeeded(next);
        }

        return Path.TrimEndingDirectorySeparator(tail.Count == 0 ? current : Path.Combine(current, Path.Combine(tail.ToArray())));
    }

    private static string ResolveLinkIfNeeded(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : new FileInfo(path);
            if ((info.Attributes & FileAttributes.ReparsePoint) == 0)
                return info.FullName;
            var target = info.ResolveLinkTarget(true);
            return target?.FullName ?? info.FullName;
        }
        catch
        {
            return Path.GetFullPath(path);
        }
    }
}
