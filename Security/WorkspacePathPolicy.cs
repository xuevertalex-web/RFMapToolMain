namespace LocalCursorAgent.Security;

public enum WorkspaceRootKind
{
    ApprovedWorkspace = 0,
    Runtime = 1,
    Scratch = 2,
    ArtifactOutput = 3
}

public enum WorkspacePathOperationKind
{
    Read = 0,
    List = 1,
    Write = 2,
    CreateDirectory = 3,
    Delete = 4,
    RenameMove = 5,
    ExtractArchive = 6,
    Execute = 7
}

public enum WorkspacePathDecisionKind
{
    Allowed = 0,
    ApprovalRequired = 1,
    Denied = 2
}

public sealed class WorkspacePathPolicyRoots
{
    public required string ApprovedWorkspaceRoot { get; init; }
    public required string RuntimeRoot { get; init; }
    public required string ScratchRoot { get; init; }
    public required string ArtifactOutputRoot { get; init; }

    public string GetRootPath(WorkspaceRootKind kind) => kind switch
    {
        WorkspaceRootKind.ApprovedWorkspace => ApprovedWorkspaceRoot,
        WorkspaceRootKind.Runtime => RuntimeRoot,
        WorkspaceRootKind.Scratch => ScratchRoot,
        WorkspaceRootKind.ArtifactOutput => ArtifactOutputRoot,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown workspace root kind")
    };
}

public sealed record WorkspacePathPolicyResult(
    WorkspacePathDecisionKind Decision,
    string ReasonCode,
    WorkspacePathOperationKind Operation,
    WorkspaceRootKind RootKind,
    string? CanonicalRootPath = null,
    string? CanonicalRequestedPath = null);

public sealed class WorkspacePathPolicy
{
    public bool AllowUncPaths { get; }

    public WorkspacePathPolicy(bool allowUncPaths = false)
    {
        AllowUncPaths = allowUncPaths;
    }

    public WorkspacePathPolicyResult Evaluate(
        WorkspacePathPolicyRoots roots,
        WorkspaceRootKind rootKind,
        WorkspacePathOperationKind operation,
        string? requestedPath)
    {
        var normalization = TryNormalizeForRoot(roots.GetRootPath(rootKind), requestedPath);
        if (!normalization.Success)
        {
            return new WorkspacePathPolicyResult(
                WorkspacePathDecisionKind.Denied,
                normalization.ReasonCode,
                operation,
                rootKind,
                normalization.CanonicalRootPath,
                normalization.CanonicalRequestedPath);
        }

        var reparseContainment = EvaluateReparseContainment(
            normalization.CanonicalRootPath!,
            normalization.CanonicalRequestedPath!,
            operation);
        if (!reparseContainment.Safe)
        {
            return new WorkspacePathPolicyResult(
                WorkspacePathDecisionKind.Denied,
                reparseContainment.ReasonCode,
                operation,
                rootKind,
                normalization.CanonicalRootPath,
                normalization.CanonicalRequestedPath);
        }

        var (decision, reasonCode) = EvaluateOperationMatrix(rootKind, operation);
        return new WorkspacePathPolicyResult(
            decision,
            reasonCode,
            operation,
            rootKind,
            normalization.CanonicalRootPath,
            normalization.CanonicalRequestedPath);
    }

    private (WorkspacePathDecisionKind Decision, string ReasonCode) EvaluateOperationMatrix(
        WorkspaceRootKind rootKind,
        WorkspacePathOperationKind operation)
    {
        return operation switch
        {
            WorkspacePathOperationKind.Read or WorkspacePathOperationKind.List =>
                (WorkspacePathDecisionKind.Allowed, "allowed"),

            WorkspacePathOperationKind.Write or WorkspacePathOperationKind.CreateDirectory =>
                rootKind == WorkspaceRootKind.Runtime
                    ? (WorkspacePathDecisionKind.Denied, "runtime_root_mutation_denied")
                    : (WorkspacePathDecisionKind.Allowed, "allowed"),

            WorkspacePathOperationKind.Delete or WorkspacePathOperationKind.RenameMove =>
                rootKind switch
                {
                    WorkspaceRootKind.ApprovedWorkspace => (WorkspacePathDecisionKind.ApprovalRequired, "destructive_requires_approval"),
                    WorkspaceRootKind.ArtifactOutput => (WorkspacePathDecisionKind.ApprovalRequired, "destructive_requires_approval"),
                    WorkspaceRootKind.Runtime => (WorkspacePathDecisionKind.Denied, "runtime_root_mutation_denied"),
                    WorkspaceRootKind.Scratch => (WorkspacePathDecisionKind.Allowed, "allowed"),
                    _ => (WorkspacePathDecisionKind.Denied, "root_kind_not_supported")
                },

            WorkspacePathOperationKind.ExtractArchive =>
                rootKind == WorkspaceRootKind.Runtime
                    ? (WorkspacePathDecisionKind.Denied, "runtime_root_extract_denied")
                    : (WorkspacePathDecisionKind.Allowed, "allowed"),

            WorkspacePathOperationKind.Execute =>
                rootKind == WorkspaceRootKind.Runtime
                    ? (WorkspacePathDecisionKind.Denied, "execute_not_authorized_by_path_policy")
                    : (WorkspacePathDecisionKind.ApprovalRequired, "execute_requires_authority_guard"),

            _ => (WorkspacePathDecisionKind.Denied, "operation_not_supported")
        };
    }

    private ReparseContainmentResult EvaluateReparseContainment(
        string canonicalRoot,
        string canonicalCandidate,
        WorkspacePathOperationKind operation)
    {
        if (!TryResolveExistingProbePath(canonicalRoot, canonicalCandidate, out var probePath, out var probeReasonCode))
            return ReparseContainmentResult.Deny(probeReasonCode ?? "reparse_state_unavailable");

        var hasReparse = false;
        foreach (var segment in EnumerateExistingSegments(canonicalRoot, probePath!))
        {
            if (!TryGetPathAttributes(segment, out var attributes))
            {
                // Fail closed: ambiguous filesystem state cannot be trusted for containment.
                return ReparseContainmentResult.Deny("reparse_state_unavailable");
            }

            if ((attributes & FileAttributes.ReparsePoint) == 0)
                continue;

            hasReparse = true;

            if (!TryResolveReparseTargetPath(segment, out var canonicalResolvedTarget))
                return ReparseContainmentResult.Deny("reparse_resolution_unavailable");

            if (!IsLexicallyContained(canonicalRoot, canonicalResolvedTarget!))
                return ReparseContainmentResult.Deny("reparse_escape_denied");
        }

        if (hasReparse && IsMutationOperation(operation))
        {
            // Mutation through any reparse chain stays fail-closed in this slice.
            return ReparseContainmentResult.Deny("reparse_mutation_denied");
        }

        return ReparseContainmentResult.Allow();
    }

    private static bool IsMutationOperation(WorkspacePathOperationKind operation) =>
        operation is WorkspacePathOperationKind.Write
            or WorkspacePathOperationKind.CreateDirectory
            or WorkspacePathOperationKind.Delete
            or WorkspacePathOperationKind.RenameMove
            or WorkspacePathOperationKind.ExtractArchive;

    private static IEnumerable<string> EnumerateExistingSegments(string canonicalRoot, string probePath)
    {
        yield return canonicalRoot;
        if (probePath.Equals(canonicalRoot, StringComparison.OrdinalIgnoreCase))
            yield break;

        var relative = Path.GetRelativePath(canonicalRoot, probePath);
        if (relative is "." or "")
            yield break;

        var segments = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        var current = canonicalRoot;
        foreach (var segment in segments)
        {
            current = NormalizeFullPath(Path.Combine(current, segment));
            yield return current;
        }
    }

    private static bool TryResolveExistingProbePath(
        string canonicalRoot,
        string canonicalCandidate,
        out string? probePath,
        out string? reasonCode)
    {
        probePath = null;
        reasonCode = null;

        if (!TryPathExists(canonicalRoot, out var rootExists, out var rootAmbiguous) || rootAmbiguous)
        {
            reasonCode = "root_state_unavailable";
            return false;
        }

        if (!rootExists)
        {
            reasonCode = "root_path_not_found";
            return false;
        }

        if (!TryPathExists(canonicalCandidate, out var candidateExists, out var candidateAmbiguous))
        {
            reasonCode = "path_state_unavailable";
            return false;
        }

        if (candidateAmbiguous)
        {
            reasonCode = "path_state_unavailable";
            return false;
        }

        if (candidateExists)
        {
            probePath = canonicalCandidate;
            return true;
        }

        var cursor = canonicalCandidate;
        while (!cursor.Equals(canonicalRoot, StringComparison.OrdinalIgnoreCase))
        {
            var parent = Path.GetDirectoryName(cursor);
            if (string.IsNullOrWhiteSpace(parent))
            {
                reasonCode = "path_parent_unavailable";
                return false;
            }

            cursor = NormalizeFullPath(parent);
            if (!IsLexicallyContained(canonicalRoot, cursor))
            {
                reasonCode = "path_outside_root";
                return false;
            }

            if (!TryPathExists(cursor, out var exists, out var ambiguous))
            {
                reasonCode = "path_state_unavailable";
                return false;
            }

            if (ambiguous)
            {
                reasonCode = "path_state_unavailable";
                return false;
            }

            if (exists)
            {
                probePath = cursor;
                return true;
            }
        }

        probePath = canonicalRoot;
        return true;
    }

    private static bool TryPathExists(string path, out bool exists, out bool ambiguous)
    {
        exists = false;
        ambiguous = false;
        try
        {
            exists = Directory.Exists(path) || File.Exists(path);
            return true;
        }
        catch
        {
            ambiguous = true;
            return false;
        }
    }

    private static bool TryGetPathAttributes(string path, out FileAttributes attributes)
    {
        attributes = default;
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveReparseTargetPath(string reparsePath, out string? canonicalResolvedTarget)
    {
        canonicalResolvedTarget = null;

        try
        {
            FileSystemInfo linkInfo = Directory.Exists(reparsePath)
                ? new DirectoryInfo(reparsePath)
                : new FileInfo(reparsePath);
            var resolved = linkInfo.ResolveLinkTarget(returnFinalTarget: true);
            if (resolved is null)
                return false;

            var fullName = resolved.FullName;
            if (string.IsNullOrWhiteSpace(fullName))
                return false;

            var resolvedPath = Path.IsPathFullyQualified(fullName)
                ? Path.GetFullPath(fullName)
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(reparsePath) ?? string.Empty, fullName));
            canonicalResolvedTarget = NormalizeFullPath(resolvedPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private NormalizationResult TryNormalizeForRoot(string rootPath, string? requestedPath)
    {
        var canonicalRoot = TryNormalizeRoot(rootPath, out var rootReasonCode);
        if (canonicalRoot is null)
            return NormalizationResult.Denied(rootReasonCode ?? "invalid_root_path");

        if (string.IsNullOrWhiteSpace(requestedPath))
            return NormalizationResult.Denied("requested_path_empty", canonicalRootPath: canonicalRoot);

        var trimmedRequest = requestedPath.Trim();

        if (PathSafetyPolicy.HasExtendedLengthPrefix(trimmedRequest))
            return NormalizationResult.Denied("extended_path_denied", canonicalRoot, null);

        if (!AllowUncPaths && PathSafetyPolicy.IsUncPath(trimmedRequest))
            return NormalizationResult.Denied("unc_path_denied", canonicalRoot, null);

        if (PathSafetyPolicy.HasRelativeDriveSyntax(trimmedRequest))
            return NormalizationResult.Denied("drive_relative_path_denied", canonicalRoot, null);

        if (PathSafetyPolicy.HasAlternateDataStreamSyntax(trimmedRequest))
            return NormalizationResult.Denied("alternate_data_stream_denied", canonicalRoot, null);

        string candidate;
        try
        {
            candidate = Path.IsPathFullyQualified(trimmedRequest)
                ? Path.GetFullPath(trimmedRequest)
                : Path.GetFullPath(Path.Combine(canonicalRoot, trimmedRequest));
        }
        catch
        {
            return NormalizationResult.Denied("path_normalization_failed", canonicalRoot, null);
        }

        var canonicalCandidate = NormalizeFullPath(candidate);
        if (!IsLexicallyContained(canonicalRoot, canonicalCandidate))
            return NormalizationResult.Denied("path_outside_root", canonicalRoot, canonicalCandidate);

        return NormalizationResult.Allowed(canonicalRoot, canonicalCandidate);
    }

    private string? TryNormalizeRoot(string? rootPath, out string? reasonCode)
    {
        reasonCode = null;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            reasonCode = "root_path_empty";
            return null;
        }

        var trimmedRoot = rootPath.Trim();
        if (PathSafetyPolicy.HasExtendedLengthPrefix(trimmedRoot))
        {
            reasonCode = "root_extended_path_denied";
            return null;
        }

        if (!AllowUncPaths && PathSafetyPolicy.IsUncPath(trimmedRoot))
        {
            reasonCode = "root_unc_path_denied";
            return null;
        }

        if (PathSafetyPolicy.HasRelativeDriveSyntax(trimmedRoot))
        {
            reasonCode = "root_drive_relative_path_denied";
            return null;
        }

        if (PathSafetyPolicy.HasAlternateDataStreamSyntax(trimmedRoot))
        {
            reasonCode = "root_alternate_data_stream_denied";
            return null;
        }

        try
        {
            return NormalizeFullPath(Path.GetFullPath(trimmedRoot));
        }
        catch
        {
            reasonCode = "root_path_normalization_failed";
            return null;
        }
    }

    private static string NormalizeFullPath(string value)
    {
        var normalized = value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return Path.TrimEndingDirectorySeparator(normalized);
    }

    private static bool IsLexicallyContained(string canonicalRoot, string canonicalCandidate)
    {
        return canonicalCandidate.Equals(canonicalRoot, StringComparison.OrdinalIgnoreCase) ||
               canonicalCandidate.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record NormalizationResult(
        bool Success,
        string ReasonCode,
        string? CanonicalRootPath,
        string? CanonicalRequestedPath)
    {
        public static NormalizationResult Allowed(string canonicalRootPath, string canonicalRequestedPath) =>
            new(true, "normalized", canonicalRootPath, canonicalRequestedPath);

        public static NormalizationResult Denied(string reasonCode, string? canonicalRootPath = null, string? canonicalRequestedPath = null) =>
            new(false, reasonCode, canonicalRootPath, canonicalRequestedPath);
    }

    private sealed record ReparseContainmentResult(bool Safe, string ReasonCode)
    {
        public static ReparseContainmentResult Allow() => new(true, "allowed");
        public static ReparseContainmentResult Deny(string reasonCode) => new(false, reasonCode);
    }
}
