namespace LocalCursorAgent.Security;

public static class AgentRuntimePaths
{
    public static string ResolveRuntimeRoot(string? baseDirectory = null)
    {
        var overrideRoot = Environment.GetEnvironmentVariable("LOCALCURSORAGENT_RUNTIME_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideRoot))
            return Path.GetFullPath(overrideRoot);

        var root = baseDirectory ?? AppContext.BaseDirectory;
        var fullRoot = Path.GetFullPath(root);
        var projectRoot = FindProjectRoot(fullRoot);
        if (!string.IsNullOrWhiteSpace(projectRoot))
            return Path.Combine(projectRoot, ".agent-runtime");

        return Path.Combine(fullRoot, ".agent-runtime");
    }

    private static string? FindProjectRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LocalCursorAgent.csproj")))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }

    public static IEnumerable<string> DefaultProtectedRoots(string runtimeRoot, string? appRoot = null)
    {
        yield return runtimeRoot;

        if (string.IsNullOrWhiteSpace(appRoot))
            yield break;

        var root = Path.GetFullPath(appRoot);
        foreach (var folderName in new[] { "bin", "obj", "logs", "agent-runtime", "vscode-extension" })
        {
            var folder = Path.Combine(root, folderName);
            yield return folder;
        }

        foreach (var fileName in new[] { "Program.cs", "LocalCursorAgent.csproj", "LocalCursorAgent.sln" })
        {
            var file = Path.Combine(root, fileName);
            yield return file;
        }
    }

    public static void EnsureRuntimeRootPrepared(string runtimeRoot)
    {
        Directory.CreateDirectory(runtimeRoot);

        try
        {
            var attributes = File.GetAttributes(runtimeRoot);
            if ((attributes & FileAttributes.Hidden) == 0)
                File.SetAttributes(runtimeRoot, attributes | FileAttributes.Hidden);
        }
        catch
        {
            // Best effort only.
        }
    }
}
