namespace LocalCursorAgent.Security;

public sealed class ProtectedPathPolicy
{
    private readonly List<string> _protectedRoots;
    private readonly PathNormalizer _normalizer = new();

    public ProtectedPathPolicy(IEnumerable<string> protectedRoots)
    {
        _protectedRoots = protectedRoots
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(_normalizer.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool IsProtected(string absolutePath)
    {
        var normalized = _normalizer.Normalize(absolutePath);
        return _protectedRoots.Any(root =>
            normalized.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    public string? GetMatchedRoot(string absolutePath)
    {
        var normalized = _normalizer.Normalize(absolutePath);
        return _protectedRoots.FirstOrDefault(root =>
            normalized.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }
}
