namespace LocalCursorAgent.Security;

public sealed class WorkspaceResolver
{
    public string? ResolveFromFile(string filePath) => ResolveAuto(filePath);

    public string? ResolveFromFolder(string folderPath) => ResolveAuto(folderPath);

    public string? ResolveFromSolution(string slnPath)
    {
        if (string.IsNullOrWhiteSpace(slnPath) || !File.Exists(slnPath))
            return null;

        return Path.GetDirectoryName(Path.GetFullPath(slnPath));
    }

    public string? ResolveFromProject(string csprojPath)
    {
        if (string.IsNullOrWhiteSpace(csprojPath) || !File.Exists(csprojPath))
            return null;

        return Path.GetDirectoryName(Path.GetFullPath(csprojPath));
    }

    public string? ResolveAuto(string seedPath)
    {
        if (string.IsNullOrWhiteSpace(seedPath))
            return null;

        var path = Path.GetFullPath(seedPath);
        if (File.Exists(path))
        {
            var ext = Path.GetExtension(path);
            if (ext.Equals(".sln", StringComparison.OrdinalIgnoreCase))
                return ResolveFromSolution(path);
            if (ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                return ResolveFromProject(path);

            path = Path.GetDirectoryName(path) ?? path;
        }

        if (Directory.Exists(path))
        {
            var current = new DirectoryInfo(path);
            while (current != null)
            {
                if (Directory.GetFiles(current.FullName, "*.sln").Any())
                    return current.FullName;
                if (Directory.GetFiles(current.FullName, "*.csproj").Any())
                    return current.FullName;
                if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                    return current.FullName;
                current = current.Parent;
            }
        }

        return null;
    }
}
