namespace LocalCursorAgent.Security;

public sealed class WorkspaceContextService
{
    private readonly WorkspaceResolver _resolver = new();
    private readonly PathNormalizer _normalizer = new();

    public WorkspaceResolutionResult Resolve(
        string? explicitWorkspace,
        string? seedPath,
        string runtimeRoot,
        IEnumerable<string>? allowedRoots = null,
        IEnumerable<string>? deniedRoots = null)
    {
        string? resolved = null;
        string? source = null;

        if (!string.IsNullOrWhiteSpace(explicitWorkspace))
        {
            source = "explicit";
            resolved = _resolver.ResolveAuto(explicitWorkspace) ?? TryFolder(explicitWorkspace);
        }
        else if (!string.IsNullOrWhiteSpace(seedPath))
        {
            source = "seed";
            resolved = _resolver.ResolveAuto(seedPath) ?? TryFolder(seedPath);
        }

        if (string.IsNullOrWhiteSpace(resolved))
        {
            return WorkspaceResolutionResult.CreateFail(
                PermissionReasonCodes.WorkspaceNotResolved,
                "Workspace root could not be resolved",
                source);
        }

        string normalizedWorkspace;
        string normalizedRuntime;
        try
        {
            normalizedWorkspace = _normalizer.Normalize(resolved);
            normalizedRuntime = _normalizer.Normalize(runtimeRoot);
        }
        catch
        {
            return WorkspaceResolutionResult.CreateFail(
                PermissionReasonCodes.PathNormalizationFailed,
                "Workspace or runtime path normalization failed",
                source);
        }

        if (IsInside(normalizedWorkspace, normalizedRuntime))
        {
            return WorkspaceResolutionResult.CreateFail(
                PermissionReasonCodes.WorkspaceRootProtected,
                "Resolved workspace points to agent runtime area",
                source,
                normalizedWorkspace);
        }

        var normalizedAllowedRoots = NormalizeAllowedRoots(allowedRoots);
        if (normalizedAllowedRoots.Count > 0 && !normalizedAllowedRoots.Any(root => IsInside(normalizedWorkspace, root)))
        {
            return WorkspaceResolutionResult.CreateFail(
                WorkspaceResolutionReasonCode.WorkspaceNotAllowed,
                "Resolved workspace is outside the configured allowlist",
                source,
                normalizedWorkspace);
        }

        var normalizedDeniedRoots = NormalizeAllowedRoots(deniedRoots);
        if (normalizedDeniedRoots.Any(root => IsInside(normalizedWorkspace, root)))
        {
            return WorkspaceResolutionResult.CreateFail(
                WorkspaceResolutionReasonCode.WorkspaceDeniedByPolicy,
                "Resolved workspace is blocked by policy",
                source,
                normalizedWorkspace);
        }

        return WorkspaceResolutionResult.CreateSuccess(normalizedWorkspace, source ?? "unknown");
    }

    private static string? TryFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var full = Path.GetFullPath(path);
        return Directory.Exists(full) ? full : null;
    }

    private static bool IsInside(string candidate, string root) =>
        candidate.Equals(root, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private List<string> NormalizeAllowedRoots(IEnumerable<string>? allowedRoots)
    {
        if (allowedRoots is null)
            return new List<string>();

        var normalized = new List<string>();
        foreach (var allowedRoot in allowedRoots.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            var resolved = _resolver.ResolveAuto(allowedRoot) ?? TryFolder(allowedRoot);
            if (string.IsNullOrWhiteSpace(resolved))
                continue;

            try
            {
                normalized.Add(_normalizer.Normalize(resolved));
            }
            catch
            {
                continue;
            }
        }

        return normalized
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed class WorkspaceResolutionResult
{
    public bool Success { get; init; }
    public WorkspaceResolutionReasonCode Reason { get; init; } = WorkspaceResolutionReasonCode.Allowed;
    public string ReasonCode { get; init; } = PermissionReasonCodes.Allowed;
    public string ReasonCodeName => Reason.ToString();
    public string Message { get; init; } = string.Empty;
    public string? WorkspaceRoot { get; init; }
    public string? Source { get; init; }

    public static WorkspaceResolutionResult CreateSuccess(string workspaceRoot, string source) => new()
    {
        Success = true,
        Reason = WorkspaceResolutionReasonCode.Allowed,
        ReasonCode = PermissionReasonCodes.Allowed,
        Message = "Workspace resolved",
        WorkspaceRoot = workspaceRoot,
        Source = source
    };

    public static WorkspaceResolutionResult CreateFail(string reasonCode, string message, string? source = null, string? workspaceRoot = null)
        => CreateFail(MapReasonCode(reasonCode), message, source, workspaceRoot);

    public static WorkspaceResolutionResult CreateFail(WorkspaceResolutionReasonCode reason, string message, string? source = null, string? workspaceRoot = null) => new()
    {
        Success = false,
        Reason = reason,
        ReasonCode = MapReasonCode(reason),
        Message = message,
        WorkspaceRoot = workspaceRoot,
        Source = source
    };

    private static string MapReasonCode(WorkspaceResolutionReasonCode reason) => reason switch
    {
        WorkspaceResolutionReasonCode.Allowed => PermissionReasonCodes.Allowed,
        WorkspaceResolutionReasonCode.WorkspaceNotResolved => PermissionReasonCodes.WorkspaceNotResolved,
        WorkspaceResolutionReasonCode.PathNormalizationFailed => PermissionReasonCodes.PathNormalizationFailed,
        WorkspaceResolutionReasonCode.WorkspaceRootProtected => PermissionReasonCodes.WorkspaceRootProtected,
        WorkspaceResolutionReasonCode.WorkspaceNotAllowed => PermissionReasonCodes.WorkspaceNotAllowed,
        WorkspaceResolutionReasonCode.WorkspaceDeniedByPolicy => PermissionReasonCodes.WorkspaceDeniedByPolicy,
        _ => PermissionReasonCodes.WorkspaceNotResolved
    };

    private static WorkspaceResolutionReasonCode MapReasonCode(string reasonCode) => reasonCode switch
    {
        PermissionReasonCodes.Allowed => WorkspaceResolutionReasonCode.Allowed,
        PermissionReasonCodes.WorkspaceNotResolved => WorkspaceResolutionReasonCode.WorkspaceNotResolved,
        PermissionReasonCodes.PathNormalizationFailed => WorkspaceResolutionReasonCode.PathNormalizationFailed,
        PermissionReasonCodes.WorkspaceRootProtected => WorkspaceResolutionReasonCode.WorkspaceRootProtected,
        PermissionReasonCodes.WorkspaceNotAllowed => WorkspaceResolutionReasonCode.WorkspaceNotAllowed,
        PermissionReasonCodes.WorkspaceDeniedByPolicy => WorkspaceResolutionReasonCode.WorkspaceDeniedByPolicy,
        _ => WorkspaceResolutionReasonCode.WorkspaceNotResolved
    };
}

public enum WorkspaceResolutionReasonCode
{
    Allowed = 0,
    WorkspaceNotResolved,
    PathNormalizationFailed,
    WorkspaceRootProtected,
    WorkspaceNotAllowed,
    WorkspaceDeniedByPolicy
}
