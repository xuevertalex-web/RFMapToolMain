using System.Text;

namespace LocalCursorAgent.Tools;

public sealed record WorkspaceFileReadResult(
    bool Success,
    string ReasonCode,
    string Content,
    bool Truncated,
    int BytesRead)
{
    public static WorkspaceFileReadResult Allowed(string content, bool truncated, int bytesRead) =>
        new(true, truncated ? "read_truncated" : "allowed", content, truncated, bytesRead);

    public static WorkspaceFileReadResult Denied(string reasonCode, int bytesRead = 0) =>
        new(false, reasonCode, string.Empty, false, bytesRead);
}

public sealed record WorkspaceDirectoryListResult(
    bool Success,
    string ReasonCode,
    IReadOnlyList<string> Entries,
    bool Truncated,
    int EntryCount)
{
    public static WorkspaceDirectoryListResult Allowed(IReadOnlyList<string> entries, bool truncated) =>
        new(true, truncated ? "list_truncated" : "allowed", entries, truncated, entries.Count);

    public static WorkspaceDirectoryListResult Denied(string reasonCode) =>
        new(false, reasonCode, Array.Empty<string>(), false, 0);
}

public sealed class WorkspaceFileAccessService
{
    public const int DefaultMaxTextBytes = 1024 * 1024;
    public const int DefaultMaxDirectoryEntries = 2000;
    public const int DefaultMaxRecursionDepth = 6;

    private readonly string _approvedWorkspaceRoot;
    private readonly string _runtimeRoot;
    private readonly int _maxTextBytes;
    private static readonly HashSet<string> DefaultSkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        "node_modules",
        "bin",
        "obj",
        ".vs",
        "dist",
        "packages",
        ".agent-runtime"
    };

    public WorkspaceFileAccessService(string approvedWorkspaceRoot, string runtimeRoot, int maxTextBytes = DefaultMaxTextBytes)
    {
        _approvedWorkspaceRoot = NormalizeRootOrEmpty(approvedWorkspaceRoot);
        _runtimeRoot = NormalizeRootOrEmpty(runtimeRoot);
        _maxTextBytes = Math.Max(1, maxTextBytes);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public WorkspaceFileReadResult ReadText(string? canonicalAuthorizedPath)
    {
        if (string.IsNullOrWhiteSpace(canonicalAuthorizedPath))
            return WorkspaceFileReadResult.Denied("read_path_unavailable");

        string normalizedPath;
        try
        {
            normalizedPath = NormalizeFullPath(Path.GetFullPath(canonicalAuthorizedPath));
        }
        catch
        {
            return WorkspaceFileReadResult.Denied("path_normalization_failed");
        }

        if (IsRuntimeStatePath(normalizedPath))
            return WorkspaceFileReadResult.Denied("runtime_state_read_denied");

        if (!File.Exists(normalizedPath))
            return WorkspaceFileReadResult.Denied("target_file_not_found");

        try
        {
            using var stream = new FileStream(
                normalizedPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            var buffer = new byte[_maxTextBytes + 1];
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (read <= 0)
                    break;
                totalRead += read;
            }

            var truncated = totalRead > _maxTextBytes;
            var boundedLength = truncated ? _maxTextBytes : totalRead;

            if (boundedLength == 0)
                return WorkspaceFileReadResult.Allowed(string.Empty, truncated, boundedLength);

            var bounded = new byte[boundedLength];
            Buffer.BlockCopy(buffer, 0, bounded, 0, boundedLength);

            if (LooksBinary(bounded))
                return WorkspaceFileReadResult.Denied("binary_file_not_supported", boundedLength);

            if (!TryDecodeText(bounded, out var decoded))
                return WorkspaceFileReadResult.Denied("binary_file_not_supported", boundedLength);

            return WorkspaceFileReadResult.Allowed(decoded!, truncated, boundedLength);
        }
        catch (UnauthorizedAccessException)
        {
            return WorkspaceFileReadResult.Denied("read_access_denied");
        }
        catch (IOException)
        {
            return WorkspaceFileReadResult.Denied("read_io_unavailable");
        }
        catch
        {
            return WorkspaceFileReadResult.Denied("read_io_unavailable");
        }
    }

    public WorkspaceDirectoryListResult ListDirectory(
        string? canonicalAuthorizedPath,
        bool recursive = false,
        int maxEntries = DefaultMaxDirectoryEntries,
        int maxRecursionDepth = DefaultMaxRecursionDepth)
    {
        if (string.IsNullOrWhiteSpace(canonicalAuthorizedPath))
            return WorkspaceDirectoryListResult.Denied("list_path_unavailable");

        string normalizedPath;
        try
        {
            normalizedPath = NormalizeFullPath(Path.GetFullPath(canonicalAuthorizedPath));
        }
        catch
        {
            return WorkspaceDirectoryListResult.Denied("path_normalization_failed");
        }

        if (IsRuntimeStatePath(normalizedPath))
            return WorkspaceDirectoryListResult.Denied("runtime_state_list_denied");

        if (!Directory.Exists(normalizedPath))
        {
            if (File.Exists(normalizedPath))
                return WorkspaceDirectoryListResult.Denied("target_not_directory");
            return WorkspaceDirectoryListResult.Denied("target_directory_not_found");
        }

        var boundedMaxEntries = Math.Max(1, maxEntries);
        var boundedMaxDepth = Math.Max(0, maxRecursionDepth);
        var entries = new List<string>(Math.Min(256, boundedMaxEntries));
        var truncated = false;

        try
        {
            var queue = new Queue<(string DirectoryPath, int Depth)>();
            queue.Enqueue((normalizedPath, 0));

            while (queue.Count > 0)
            {
                var (currentDirectory, depth) = queue.Dequeue();
                IEnumerable<string> children = Directory.EnumerateFileSystemEntries(currentDirectory);
                var orderedChildren = children
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x, StringComparer.Ordinal);

                foreach (var child in orderedChildren)
                {
                    var childName = Path.GetFileName(child);
                    var childIsDirectory = Directory.Exists(child);

                    if (childIsDirectory && IsSkippedDirectoryName(childName))
                        continue;

                    var relative = NormalizeListEntry(Path.GetRelativePath(normalizedPath, child), childIsDirectory);
                    if (string.IsNullOrWhiteSpace(relative) || relative == ".")
                        continue;

                    entries.Add(relative);
                    if (entries.Count >= boundedMaxEntries)
                    {
                        truncated = true;
                        break;
                    }

                    if (recursive &&
                        childIsDirectory &&
                        depth < boundedMaxDepth &&
                        !IsReparsePointDirectory(child))
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }

                if (truncated)
                    break;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return WorkspaceDirectoryListResult.Denied("list_access_denied");
        }
        catch (IOException)
        {
            return WorkspaceDirectoryListResult.Denied("list_io_unavailable");
        }
        catch
        {
            return WorkspaceDirectoryListResult.Denied("list_io_unavailable");
        }

        return WorkspaceDirectoryListResult.Allowed(entries, truncated);
    }

    private bool IsRuntimeStatePath(string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(_approvedWorkspaceRoot))
            return IsWithin(normalizedPath, _runtimeRoot);

        if (IsWithin(normalizedPath, _approvedWorkspaceRoot))
        {
            var runtimeDiagnostics = NormalizeFullPath(Path.Combine(_approvedWorkspaceRoot, ".agent-runtime"));
            return IsWithin(normalizedPath, runtimeDiagnostics);
        }

        if (IsWithin(normalizedPath, _runtimeRoot))
            return true;

        if (string.IsNullOrWhiteSpace(_approvedWorkspaceRoot))
            return false;

        return false;
    }

    private static bool IsWithin(string candidate, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return false;

        return candidate.Equals(root, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRootOrEmpty(string? root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return string.Empty;
        try
        {
            return NormalizeFullPath(Path.GetFullPath(root));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizeFullPath(string value)
    {
        var normalized = value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return Path.TrimEndingDirectorySeparator(normalized);
    }

    private static string NormalizeListEntry(string relativePath, bool isDirectory)
    {
        var normalized = relativePath
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        if (isDirectory && !normalized.EndsWith("/", StringComparison.Ordinal))
            normalized += "/";
        return normalized;
    }

    private static bool IsReparsePointDirectory(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsSkippedDirectoryName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && DefaultSkippedDirectoryNames.Contains(name);

    private static bool LooksBinary(byte[] bytes)
    {
        if (bytes.Length == 0)
            return false;

        var sampleLength = Math.Min(bytes.Length, 4096);
        var suspicious = 0;
        for (var i = 0; i < sampleLength; i++)
        {
            var b = bytes[i];
            if (b == 0)
                return true;

            if (b < 0x09 || (b > 0x0D && b < 0x20))
                suspicious++;
        }

        return suspicious > sampleLength / 10;
    }

    private static bool TryDecodeText(byte[] bytes, out string? text)
    {
        text = null;

        try
        {
            if (bytes.Length >= 3 &&
                bytes[0] == 0xEF &&
                bytes[1] == 0xBB &&
                bytes[2] == 0xBF)
            {
                text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true)
                    .GetString(bytes, 3, bytes.Length - 3);
                return true;
            }

            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
            return true;
        }
        catch
        {
            try
            {
                text = Encoding.GetEncoding(1251).GetString(bytes);
                return true;
            }
            catch
            {
                text = null;
                return false;
            }
        }
    }
}
