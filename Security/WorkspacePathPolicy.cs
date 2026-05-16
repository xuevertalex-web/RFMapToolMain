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
}
