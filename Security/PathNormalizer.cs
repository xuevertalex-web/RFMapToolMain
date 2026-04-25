namespace LocalCursorAgent.Security;

public sealed class PathNormalizer
{
    public string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is empty", nameof(path));

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    public bool IsUnderRoot(string candidatePath, string rootPath)
    {
        var candidate = Normalize(candidatePath);
        var root = Normalize(rootPath);

        return candidate.Equals(root, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
