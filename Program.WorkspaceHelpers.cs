internal static class ProgramWorkspaceHelpers
{
    public static string? FindDefaultWorkspacePolicyPath(string appRoot)
    {
        var candidate = Path.Combine(appRoot, "agent-policy.json");
        return File.Exists(candidate) ? candidate : null;
    }

    public static string? ExtractWorkspacePathFromTask(string? task)
    {
        if (string.IsNullOrWhiteSpace(task))
            return null;

        var matches = System.Text.RegularExpressions.Regex.Matches(
            task,
            @"[A-Za-z]:\\[^\r\n\t\""<>|]*");

        foreach (System.Text.RegularExpressions.Match match in matches.Cast<System.Text.RegularExpressions.Match>().OrderByDescending(m => m.Value.Length))
        {
            var candidate = match.Value.Trim().TrimEnd('.', ',', ';', ':');
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            try
            {
                var fullPath = Path.GetFullPath(candidate);
                if (Directory.Exists(fullPath))
                    return fullPath;

                var parent = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                    return parent;
            }
            catch
            {
                continue;
            }
        }

        return null;
    }

    public static List<string> MergeDistinct(IEnumerable<string>? first, IEnumerable<string>? second)
    {
        return (first ?? Enumerable.Empty<string>())
            .Concat(second ?? Enumerable.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
