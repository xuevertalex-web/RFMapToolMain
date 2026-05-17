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

public sealed record WorkspaceFileSearchResult(
    bool Success,
    string ReasonCode,
    IReadOnlyList<string> Matches,
    bool Truncated,
    int ResultCount,
    int VisitedFiles,
    int VisitedDirectories,
    IReadOnlyList<string> BudgetsHit)
{
    public static WorkspaceFileSearchResult Allowed(
        IReadOnlyList<string> matches,
        bool truncated,
        int visitedFiles,
        int visitedDirectories,
        IReadOnlyList<string> budgetsHit)
    {
        var reasonCode = truncated
            ? "search_budget_exceeded"
            : "allowed";
        return new(
            true,
            reasonCode,
            matches,
            truncated,
            matches.Count,
            visitedFiles,
            visitedDirectories,
            budgetsHit);
    }

    public static WorkspaceFileSearchResult Denied(string reasonCode) =>
        new(false, reasonCode, Array.Empty<string>(), false, 0, 0, 0, Array.Empty<string>());
}

public sealed class WorkspaceFileAccessService
{
    public const int DefaultMaxTextBytes = 1024 * 1024;
    public const int DefaultMaxDirectoryEntries = 2000;
    public const int DefaultMaxRecursionDepth = 6;
    public const int DefaultMaxVisitedFiles = 20000;
    public const int DefaultMaxSearchResults = 500;
    public const int DefaultMaxPatternLength = 256;

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

    public WorkspaceFileSearchResult SearchFiles(
        string? canonicalAuthorizedPath,
        string? namePattern,
        bool recursive = false,
        int maxDepth = DefaultMaxRecursionDepth,
        int maxVisitedFiles = DefaultMaxVisitedFiles,
        int maxResults = DefaultMaxSearchResults,
        string patternMode = "literal",
        bool? caseSensitive = null,
        int maxPatternLength = DefaultMaxPatternLength)
    {
        if (string.IsNullOrWhiteSpace(canonicalAuthorizedPath))
            return WorkspaceFileSearchResult.Denied("search_path_unavailable");

        string normalizedPath;
        try
        {
            normalizedPath = NormalizeFullPath(Path.GetFullPath(canonicalAuthorizedPath));
        }
        catch
        {
            return WorkspaceFileSearchResult.Denied("path_normalization_failed");
        }

        if (IsRuntimeStatePath(normalizedPath))
            return WorkspaceFileSearchResult.Denied("runtime_state_search_denied");

        if (!Directory.Exists(normalizedPath))
        {
            if (File.Exists(normalizedPath))
                return WorkspaceFileSearchResult.Denied("target_not_directory");
            return WorkspaceFileSearchResult.Denied("target_directory_not_found");
        }

        var normalizedPattern = namePattern?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPattern))
            return WorkspaceFileSearchResult.Denied("search_pattern_invalid");

        if (normalizedPattern.Length > Math.Max(1, maxPatternLength))
            return WorkspaceFileSearchResult.Denied("search_pattern_invalid");

        var normalizedMode = (patternMode ?? "literal").Trim().ToLowerInvariant();
        if (normalizedMode is not ("literal" or "simple_glob"))
            return WorkspaceFileSearchResult.Denied("search_pattern_invalid");

        var useCaseSensitive = caseSensitive ?? !OperatingSystem.IsWindows();
        var boundedMaxDepth = Math.Max(0, maxDepth);
        var boundedMaxVisitedFiles = Math.Max(1, maxVisitedFiles);
        var boundedMaxResults = Math.Max(1, maxResults);

        var matches = new List<string>(Math.Min(64, boundedMaxResults));
        var budgetsHit = new HashSet<string>(StringComparer.Ordinal);
        var visitedFiles = 0;
        var visitedDirectories = 0;
        var truncated = false;

        var workspacePathPolicy = new LocalCursorAgent.Security.WorkspacePathPolicy();
        var policyRoots = new LocalCursorAgent.Security.WorkspacePathPolicyRoots
        {
            ApprovedWorkspaceRoot = _approvedWorkspaceRoot,
            RuntimeRoot = _runtimeRoot,
            ScratchRoot = Path.Combine(_approvedWorkspaceRoot, ".scratch"),
            ArtifactOutputRoot = Path.Combine(_approvedWorkspaceRoot, ".artifacts")
        };

        try
        {
            var queue = new Queue<(string DirectoryPath, int Depth)>();
            queue.Enqueue((normalizedPath, 0));

            while (queue.Count > 0)
            {
                var (currentDirectory, depth) = queue.Dequeue();
                visitedDirectories++;

                IEnumerable<string> children = Directory.EnumerateFileSystemEntries(currentDirectory);
                var orderedChildren = children
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x, StringComparer.Ordinal);

                foreach (var child in orderedChildren)
                {
                    var childName = Path.GetFileName(child);
                    var childIsDirectory = Directory.Exists(child);
                    var normalizedChild = NormalizeFullPath(Path.GetFullPath(child));

                    if (childIsDirectory)
                    {
                        if (IsSkippedDirectoryName(childName))
                            continue;

                        if (recursive)
                        {
                            if (depth >= boundedMaxDepth)
                            {
                                truncated = true;
                                budgetsHit.Add("max_depth");
                                continue;
                            }

                            if (!IsReparsePointDirectory(child))
                                queue.Enqueue((child, depth + 1));
                        }

                        continue;
                    }

                    if (visitedFiles >= boundedMaxVisitedFiles)
                    {
                        truncated = true;
                        budgetsHit.Add("max_visited_files");
                        break;
                    }
                    visitedFiles++;

                    var candidateDecision = workspacePathPolicy.Evaluate(
                        policyRoots,
                        LocalCursorAgent.Security.WorkspaceRootKind.ApprovedWorkspace,
                        LocalCursorAgent.Security.WorkspacePathOperationKind.List,
                        normalizedChild);
                    if (candidateDecision.Decision != LocalCursorAgent.Security.WorkspacePathDecisionKind.Allowed)
                        continue;

                    if (!NameMatchesPattern(childName, normalizedPattern, normalizedMode, useCaseSensitive))
                        continue;

                    var relative = ToWorkspaceRelativePath(normalizedChild);
                    if (string.IsNullOrWhiteSpace(relative))
                        continue;

                    matches.Add(relative);
                    if (matches.Count >= boundedMaxResults)
                    {
                        truncated = true;
                        budgetsHit.Add("max_results");
                        break;
                    }
                }

                if (truncated && budgetsHit.Count > 0)
                    break;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return WorkspaceFileSearchResult.Denied("search_access_denied");
        }
        catch (IOException)
        {
            return WorkspaceFileSearchResult.Denied("search_io_unavailable");
        }
        catch
        {
            return WorkspaceFileSearchResult.Denied("search_io_unavailable");
        }

        return WorkspaceFileSearchResult.Allowed(
            matches,
            truncated,
            visitedFiles,
            visitedDirectories,
            budgetsHit.OrderBy(x => x, StringComparer.Ordinal).ToArray());
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

    private string? ToWorkspaceRelativePath(string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(_approvedWorkspaceRoot))
            return null;

        if (!IsWithin(normalizedPath, _approvedWorkspaceRoot))
            return null;

        var relative = Path.GetRelativePath(_approvedWorkspaceRoot, normalizedPath);
        if (string.IsNullOrWhiteSpace(relative) || relative == ".")
            return null;

        return relative
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static bool NameMatchesPattern(string fileName, string pattern, string mode, bool caseSensitive)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return mode switch
        {
            "literal" => fileName.Contains(pattern, comparison),
            "simple_glob" => WildcardMatch(fileName, pattern, caseSensitive),
            _ => false
        };
    }

    private static bool WildcardMatch(string candidate, string pattern, bool caseSensitive)
    {
        var text = caseSensitive ? candidate : candidate.ToUpperInvariant();
        var mask = caseSensitive ? pattern : pattern.ToUpperInvariant();

        var textIndex = 0;
        var maskIndex = 0;
        var starIndex = -1;
        var matchIndex = 0;

        while (textIndex < text.Length)
        {
            if (maskIndex < mask.Length && (mask[maskIndex] == '?' || mask[maskIndex] == text[textIndex]))
            {
                textIndex++;
                maskIndex++;
                continue;
            }

            if (maskIndex < mask.Length && mask[maskIndex] == '*')
            {
                starIndex = maskIndex++;
                matchIndex = textIndex;
                continue;
            }

            if (starIndex >= 0)
            {
                maskIndex = starIndex + 1;
                textIndex = ++matchIndex;
                continue;
            }

            return false;
        }

        while (maskIndex < mask.Length && mask[maskIndex] == '*')
            maskIndex++;

        return maskIndex == mask.Length;
    }

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
