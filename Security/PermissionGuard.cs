namespace LocalCursorAgent.Security;

public sealed class PermissionGuard
{
    private readonly PathNormalizer _paths = new();

    public PermissionDecision Evaluate(AgentSessionContext session, ToolAction action)
    {
        if (string.IsNullOrWhiteSpace(session.RuntimeRoot) || string.IsNullOrWhiteSpace(session.ActiveWorkspaceRoot))
            return PermissionDecision.Deny(PermissionReasonCode.WorkspaceNotResolved, "Workspace root not resolved");

        if (HasExtendedLengthPrefix(action.TargetPath))
            return PermissionDecision.Deny(PermissionReasonCode.ExtendedLengthPathDenied, "Target uses extended-length path prefix", action.TargetPath, session.ActiveWorkspaceRoot);

        if (HasExtendedLengthPrefix(action.SourcePath))
            return PermissionDecision.Deny(PermissionReasonCode.ExtendedLengthPathDenied, "Source uses extended-length path prefix", action.SourcePath, session.ActiveWorkspaceRoot);

        if (HasExtendedLengthPrefix(action.DestinationPath))
            return PermissionDecision.Deny(PermissionReasonCode.ExtendedLengthPathDenied, "Destination uses extended-length path prefix", action.DestinationPath, session.ActiveWorkspaceRoot);

        if (HasExtendedLengthPrefix(action.WorkingDirectory))
            return PermissionDecision.Deny(PermissionReasonCode.ExtendedLengthPathDenied, "Working directory uses extended-length path prefix", action.WorkingDirectory, session.ActiveWorkspaceRoot);

        if (HasRelativeDriveSyntax(action.TargetPath))
            return PermissionDecision.Deny(PermissionReasonCode.InvalidPathSyntaxDenied, "Target uses drive-relative syntax", action.TargetPath, session.ActiveWorkspaceRoot);

        if (HasRelativeDriveSyntax(action.SourcePath))
            return PermissionDecision.Deny(PermissionReasonCode.InvalidPathSyntaxDenied, "Source uses drive-relative syntax", action.SourcePath, session.ActiveWorkspaceRoot);

        if (HasRelativeDriveSyntax(action.DestinationPath))
            return PermissionDecision.Deny(PermissionReasonCode.InvalidPathSyntaxDenied, "Destination uses drive-relative syntax", action.DestinationPath, session.ActiveWorkspaceRoot);

        if (HasRelativeDriveSyntax(action.WorkingDirectory))
            return PermissionDecision.Deny(PermissionReasonCode.InvalidPathSyntaxDenied, "Working directory uses drive-relative syntax", action.WorkingDirectory, session.ActiveWorkspaceRoot);

        if (HasAlternateDataStreamSyntax(action.TargetPath))
            return PermissionDecision.Deny(PermissionReasonCode.AlternateDataStreamDenied, "Target uses alternate data stream syntax", action.TargetPath, session.ActiveWorkspaceRoot);

        if (HasAlternateDataStreamSyntax(action.SourcePath))
            return PermissionDecision.Deny(PermissionReasonCode.AlternateDataStreamDenied, "Source uses alternate data stream syntax", action.SourcePath, session.ActiveWorkspaceRoot);

        if (HasAlternateDataStreamSyntax(action.DestinationPath))
            return PermissionDecision.Deny(PermissionReasonCode.AlternateDataStreamDenied, "Destination uses alternate data stream syntax", action.DestinationPath, session.ActiveWorkspaceRoot);

        if (HasAlternateDataStreamSyntax(action.WorkingDirectory))
            return PermissionDecision.Deny(PermissionReasonCode.AlternateDataStreamDenied, "Working directory uses alternate data stream syntax", action.WorkingDirectory, session.ActiveWorkspaceRoot);

        string normalizedWorkspace;
        string? normalizedTarget = null;
        string? normalizedSource = null;
        string? normalizedDestination = null;

        try
        {
            normalizedWorkspace = _paths.Normalize(session.ActiveWorkspaceRoot);

            if (!string.IsNullOrWhiteSpace(action.TargetPath))
                normalizedTarget = _paths.Normalize(action.TargetPath);
            if (!string.IsNullOrWhiteSpace(action.SourcePath))
                normalizedSource = _paths.Normalize(action.SourcePath);
            if (!string.IsNullOrWhiteSpace(action.DestinationPath))
                normalizedDestination = _paths.Normalize(action.DestinationPath);
            else if (!string.IsNullOrWhiteSpace(action.WorkingDirectory))
                normalizedTarget = _paths.Normalize(action.WorkingDirectory);
        }
        catch
        {
            return PermissionDecision.Deny(PermissionReasonCode.PathNormalizationFailed, "Path normalization failed");
        }

        if (normalizedTarget is not null && IsUncPath(normalizedTarget))
            return PermissionDecision.Deny(PermissionReasonCode.NetworkPathDenied, "Target is a network path", normalizedTarget, normalizedWorkspace);

        if (normalizedSource is not null && IsUncPath(normalizedSource))
            return PermissionDecision.Deny(PermissionReasonCode.NetworkPathDenied, "Source is a network path", normalizedSource, normalizedWorkspace);

        if (normalizedDestination is not null && IsUncPath(normalizedDestination))
            return PermissionDecision.Deny(PermissionReasonCode.NetworkPathDenied, "Destination is a network path", normalizedDestination, normalizedWorkspace);

        if (normalizedTarget is not null && !IsWithinWorkspace(normalizedTarget, normalizedWorkspace))
            return CreateApprovalRequired(action, "Target is outside active workspace", normalizedTarget, normalizedWorkspace);

        if (normalizedSource is not null && !IsWithinWorkspace(normalizedSource, normalizedWorkspace))
            return CreateApprovalRequired(action, "Source is outside active workspace", normalizedSource, normalizedWorkspace);

        if (normalizedDestination is not null && !IsWithinWorkspace(normalizedDestination, normalizedWorkspace))
            return CreateApprovalRequired(action, "Destination is outside active workspace", normalizedDestination, normalizedWorkspace);

        if (action.Kind == ToolActionKind.RunCommand)
            return PermissionDecision.Allow(normalizedTarget ?? normalizedWorkspace, normalizedWorkspace);

        if (normalizedTarget is not null && session.ProtectedPathPolicy.IsProtected(normalizedTarget))
            return PermissionDecision.Deny(PermissionReasonCode.ProtectedPathDenied, "Target is protected", normalizedTarget, normalizedWorkspace);

        if (normalizedSource is not null && session.ProtectedPathPolicy.IsProtected(normalizedSource))
            return PermissionDecision.Deny(PermissionReasonCode.ProtectedPathDenied, "Source is protected", normalizedSource, normalizedWorkspace);

        if (normalizedDestination is not null && session.ProtectedPathPolicy.IsProtected(normalizedDestination))
            return PermissionDecision.Deny(PermissionReasonCode.ProtectedPathDenied, "Destination is protected", normalizedDestination, normalizedWorkspace);

        if (normalizedTarget is not null && ContainsReparsePoint(normalizedTarget))
            return PermissionDecision.Deny(PermissionReasonCode.ReparsePointDenied, "Target path contains a reparse point", normalizedTarget, normalizedWorkspace);

        if (normalizedSource is not null && ContainsReparsePoint(normalizedSource))
            return PermissionDecision.Deny(PermissionReasonCode.ReparsePointDenied, "Source path contains a reparse point", normalizedSource, normalizedWorkspace);

        if (normalizedDestination is not null && ContainsReparsePoint(normalizedDestination))
            return PermissionDecision.Deny(PermissionReasonCode.ReparsePointDenied, "Destination path contains a reparse point", normalizedDestination, normalizedWorkspace);

        if (session.AccessMode == AgentAccessMode.ReadOnly)
        {
            if (IsWriteLike(action.Kind))
                return PermissionDecision.Deny(PermissionReasonCode.ReadOnlyWriteDenied, "Write denied in ReadOnly mode", normalizedTarget, normalizedWorkspace);

            if (IsDeleteLike(action.Kind))
                return PermissionDecision.Deny(PermissionReasonCode.ReadOnlyDeleteDenied, "Delete denied in ReadOnly mode", normalizedTarget, normalizedWorkspace);

            if (IsRenameLike(action.Kind) || IsMoveLike(action.Kind))
                return PermissionDecision.Deny(PermissionReasonCode.ReadOnlyWriteDenied, "Rename/move denied in ReadOnly mode", normalizedTarget, normalizedWorkspace);
        }

        if (session.AccessMode == AgentAccessMode.WorkspaceWrite)
        {
            if (IsDeleteLike(action.Kind) || IsRenameLike(action.Kind) || IsMoveLike(action.Kind))
                return PermissionDecision.Deny(PermissionReasonCode.WriteModeDeleteDenied, "Destructive operations denied in WorkspaceWrite mode", normalizedTarget, normalizedWorkspace);
        }

        if ((action.Kind == ToolActionKind.Build || action.Kind == ToolActionKind.Test) &&
            !string.IsNullOrWhiteSpace(normalizedTarget) &&
            !Directory.Exists(normalizedTarget))
        {
            return PermissionDecision.Deny(PermissionReasonCode.InvalidWorkingDirectory, "Working directory does not exist", normalizedTarget, normalizedWorkspace);
        }

        if ((action.Kind == ToolActionKind.RenameFile || action.Kind == ToolActionKind.MoveFile) &&
            string.IsNullOrWhiteSpace(normalizedSource))
        {
            return PermissionDecision.Deny(PermissionReasonCode.PathNormalizationFailed, "Source path is required for rename/move");
        }

        if ((action.Kind == ToolActionKind.RenameFile || action.Kind == ToolActionKind.MoveFile) &&
            string.IsNullOrWhiteSpace(normalizedDestination))
        {
            return PermissionDecision.Deny(PermissionReasonCode.PathNormalizationFailed, "Destination path is required for rename/move");
        }

        var resolvedTarget = normalizedTarget ?? normalizedSource ?? normalizedDestination;
        return PermissionDecision.Allow(resolvedTarget, normalizedWorkspace);
    }

    private static bool IsWithinWorkspace(string path, string workspaceRoot) =>
        path.Equals(workspaceRoot, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(workspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool IsWriteLike(ToolActionKind kind) =>
        kind is ToolActionKind.WriteFile or ToolActionKind.CreateFile or ToolActionKind.PatchFile;

    private static bool IsDeleteLike(ToolActionKind kind) => kind == ToolActionKind.DeleteFile;
    private static bool IsRenameLike(ToolActionKind kind) => kind == ToolActionKind.RenameFile;
    private static bool IsMoveLike(ToolActionKind kind) => kind == ToolActionKind.MoveFile;

    private static bool IsUncPath(string path) =>
        path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("//", StringComparison.OrdinalIgnoreCase);

    private static bool IsPathSeparator(char ch) => ch == '\\' || ch == '/';

    private static bool HasExtendedLengthPrefix(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ||
         path.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase));

    private static bool HasAlternateDataStreamSyntax(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (HasExtendedLengthPrefix(path) || IsUncPath(path))
            return false;

        var firstColon = path.IndexOf(':');
        if (firstColon < 0)
            return false;

        if (firstColon == 1 && path.Length >= 3 && IsPathSeparator(path[2]))
            return path.IndexOf(':', 3) >= 0;

        return true;
    }

    private static bool HasRelativeDriveSyntax(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Length >= 2 &&
               char.IsLetter(path[0]) &&
               path[1] == ':' &&
               (path.Length == 2 || !IsPathSeparator(path[2]));
    }

    private static bool ContainsReparsePoint(string path)
    {
        var current = new DirectoryInfo(path);

        if (File.Exists(path))
            current = new DirectoryInfo(Path.GetDirectoryName(path) ?? path);

        while (current != null)
        {
            if (!current.Exists)
            {
                current = current.Parent;
                continue;
            }

            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;

            current = current.Parent;
        }

        return false;
    }

    private static PermissionDecision CreateApprovalRequired(ToolAction action, string message, string normalizedTarget, string normalizedWorkspace)
    {
        var proposal = new ActionApprovalProposal
        {
            ActionType = action.Kind.ToString(),
            Command = action.Kind == ToolActionKind.RunCommand ? action.Payload : null,
            Path = action.TargetPath ?? action.SourcePath ?? action.DestinationPath ?? action.WorkingDirectory,
            NormalizedTarget = normalizedTarget,
            SandboxRoot = normalizedWorkspace,
            ProjectRoot = normalizedWorkspace,
            WorktreeRoot = normalizedWorkspace,
            IsInsideSandbox = false,
            RiskLevel = "high",
            Reason = message,
            RequiresApproval = true,
            ApprovalStatus = ApprovalStatus.ApprovalRequired
        };

        return PermissionDecision.ApprovalRequired(
            PermissionReasonCode.PathOutsideWorkspace,
            message,
            proposal,
            normalizedTarget,
            normalizedWorkspace);
    }
}
