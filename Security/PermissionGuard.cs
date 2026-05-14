namespace LocalCursorAgent.Security;

public sealed class PermissionGuard
{
    private readonly PathNormalizer _paths = new();

    public PermissionDecision Evaluate(AgentSessionContext session, ToolAction action)
    {
        var workspaceRoot = session.ExecutionWorkspaceRoot ?? session.ActiveWorkspaceRoot;
        if (string.IsNullOrWhiteSpace(session.RuntimeRoot) || string.IsNullOrWhiteSpace(workspaceRoot))
            return PermissionDecision.Deny(PermissionReasonCode.WorkspaceNotResolved, "Workspace root not resolved");

        if (PathSafetyPolicy.HasExtendedLengthPrefix(action.TargetPath))
            return PermissionDecision.Deny(PermissionReasonCode.ExtendedLengthPathDenied, "Target uses extended-length path prefix", action.TargetPath, workspaceRoot);

        if (PathSafetyPolicy.HasExtendedLengthPrefix(action.SourcePath))
            return PermissionDecision.Deny(PermissionReasonCode.ExtendedLengthPathDenied, "Source uses extended-length path prefix", action.SourcePath, session.ActiveWorkspaceRoot);

        if (PathSafetyPolicy.HasExtendedLengthPrefix(action.DestinationPath))
            return PermissionDecision.Deny(PermissionReasonCode.ExtendedLengthPathDenied, "Destination uses extended-length path prefix", action.DestinationPath, session.ActiveWorkspaceRoot);

        if (PathSafetyPolicy.HasExtendedLengthPrefix(action.WorkingDirectory))
            return PermissionDecision.Deny(PermissionReasonCode.ExtendedLengthPathDenied, "Working directory uses extended-length path prefix", action.WorkingDirectory, session.ActiveWorkspaceRoot);

        if (PathSafetyPolicy.HasRelativeDriveSyntax(action.TargetPath))
            return PermissionDecision.Deny(PermissionReasonCode.InvalidPathSyntaxDenied, "Target uses drive-relative syntax", action.TargetPath, session.ActiveWorkspaceRoot);

        if (PathSafetyPolicy.HasRelativeDriveSyntax(action.SourcePath))
            return PermissionDecision.Deny(PermissionReasonCode.InvalidPathSyntaxDenied, "Source uses drive-relative syntax", action.SourcePath, session.ActiveWorkspaceRoot);

        if (PathSafetyPolicy.HasRelativeDriveSyntax(action.DestinationPath))
            return PermissionDecision.Deny(PermissionReasonCode.InvalidPathSyntaxDenied, "Destination uses drive-relative syntax", action.DestinationPath, session.ActiveWorkspaceRoot);

        if (PathSafetyPolicy.HasRelativeDriveSyntax(action.WorkingDirectory))
            return PermissionDecision.Deny(PermissionReasonCode.InvalidPathSyntaxDenied, "Working directory uses drive-relative syntax", action.WorkingDirectory, session.ActiveWorkspaceRoot);

        if (PathSafetyPolicy.HasAlternateDataStreamSyntax(action.TargetPath))
            return PermissionDecision.Deny(PermissionReasonCode.AlternateDataStreamDenied, "Target uses alternate data stream syntax", action.TargetPath, session.ActiveWorkspaceRoot);

        if (PathSafetyPolicy.HasAlternateDataStreamSyntax(action.SourcePath))
            return PermissionDecision.Deny(PermissionReasonCode.AlternateDataStreamDenied, "Source uses alternate data stream syntax", action.SourcePath, session.ActiveWorkspaceRoot);

        if (PathSafetyPolicy.HasAlternateDataStreamSyntax(action.DestinationPath))
            return PermissionDecision.Deny(PermissionReasonCode.AlternateDataStreamDenied, "Destination uses alternate data stream syntax", action.DestinationPath, session.ActiveWorkspaceRoot);

        if (PathSafetyPolicy.HasAlternateDataStreamSyntax(action.WorkingDirectory))
            return PermissionDecision.Deny(PermissionReasonCode.AlternateDataStreamDenied, "Working directory uses alternate data stream syntax", action.WorkingDirectory, session.ActiveWorkspaceRoot);

        string normalizedWorkspace;
        string? normalizedTarget = null;
        string? normalizedSource = null;
        string? normalizedDestination = null;

        try
        {
            normalizedWorkspace = _paths.Normalize(workspaceRoot);

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

        if (normalizedTarget is not null && PathSafetyPolicy.IsUncPath(normalizedTarget))
            return PermissionDecision.Deny(PermissionReasonCode.NetworkPathDenied, "Target is a network path", normalizedTarget, normalizedWorkspace);

        if (normalizedSource is not null && PathSafetyPolicy.IsUncPath(normalizedSource))
            return PermissionDecision.Deny(PermissionReasonCode.NetworkPathDenied, "Source is a network path", normalizedSource, normalizedWorkspace);

        if (normalizedDestination is not null && PathSafetyPolicy.IsUncPath(normalizedDestination))
            return PermissionDecision.Deny(PermissionReasonCode.NetworkPathDenied, "Destination is a network path", normalizedDestination, normalizedWorkspace);

        if (normalizedTarget is not null && !IsWithinWorkspace(normalizedTarget, normalizedWorkspace))
            return CreateApprovalRequired(action, PermissionReasonCode.PathOutsideWorkspace, "Target is outside active workspace", normalizedTarget, normalizedWorkspace);

        if (normalizedSource is not null && !IsWithinWorkspace(normalizedSource, normalizedWorkspace))
            return CreateApprovalRequired(action, PermissionReasonCode.PathOutsideWorkspace, "Source is outside active workspace", normalizedSource, normalizedWorkspace);

        if (normalizedDestination is not null && !IsWithinWorkspace(normalizedDestination, normalizedWorkspace))
            return CreateApprovalRequired(action, PermissionReasonCode.PathOutsideWorkspace, "Destination is outside active workspace", normalizedDestination, normalizedWorkspace);

        if (action.Kind == ToolActionKind.RunCommand)
        {
            var hasApprovalMarker = CommandRiskPolicy.HasExplicitApprovalMarker(action.Payload);
            if (CommandRiskPolicy.IsHighRiskCommand(action.Payload) && !hasApprovalMarker)
                return CreateApprovalRequired(action, PermissionReasonCode.HighRiskApprovalRequired, "High-risk host/system/network-impacting command requires explicit approval", normalizedTarget ?? normalizedWorkspace, normalizedWorkspace, CommandRiskPolicy.ResolveCommandRiskLevel(action.Payload));

            return PermissionDecision.Allow(normalizedTarget ?? normalizedWorkspace, normalizedWorkspace);
        }

        if (normalizedTarget is not null && session.ProtectedPathPolicy.IsProtected(normalizedTarget) && !IsWithinExecutionWorkspace(normalizedTarget, session))
            return PermissionDecision.Deny(PermissionReasonCode.ProtectedPathDenied, "Target is protected", normalizedTarget, normalizedWorkspace);

        if (normalizedSource is not null && session.ProtectedPathPolicy.IsProtected(normalizedSource) && !IsWithinExecutionWorkspace(normalizedSource, session))
            return PermissionDecision.Deny(PermissionReasonCode.ProtectedPathDenied, "Source is protected", normalizedSource, normalizedWorkspace);

        if (normalizedDestination is not null && session.ProtectedPathPolicy.IsProtected(normalizedDestination) && !IsWithinExecutionWorkspace(normalizedDestination, session))
            return PermissionDecision.Deny(PermissionReasonCode.ProtectedPathDenied, "Destination is protected", normalizedDestination, normalizedWorkspace);

        if (normalizedTarget is not null && PathSafetyPolicy.ContainsReparsePoint(normalizedTarget))
            return PermissionDecision.Deny(PermissionReasonCode.ReparsePointDenied, "Target path contains a reparse point", normalizedTarget, normalizedWorkspace);

        if (normalizedSource is not null && PathSafetyPolicy.ContainsReparsePoint(normalizedSource))
            return PermissionDecision.Deny(PermissionReasonCode.ReparsePointDenied, "Source path contains a reparse point", normalizedSource, normalizedWorkspace);

        if (normalizedDestination is not null && PathSafetyPolicy.ContainsReparsePoint(normalizedDestination))
            return PermissionDecision.Deny(PermissionReasonCode.ReparsePointDenied, "Destination path contains a reparse point", normalizedDestination, normalizedWorkspace);

        if (IsRuntimeDiagnosticsMutation(action.Kind))
        {
            if (normalizedTarget is not null && IsWithinRuntimeDiagnosticsPath(normalizedTarget, normalizedWorkspace))
                return PermissionDecision.Deny(PermissionReasonCode.ProtectedRuntimeDiagnosticsPathDenied, "Mutation denied for protected runtime diagnostics path", normalizedTarget, normalizedWorkspace);

            if (normalizedSource is not null && IsWithinRuntimeDiagnosticsPath(normalizedSource, normalizedWorkspace))
                return PermissionDecision.Deny(PermissionReasonCode.ProtectedRuntimeDiagnosticsPathDenied, "Mutation denied for protected runtime diagnostics path", normalizedSource, normalizedWorkspace);

            if (normalizedDestination is not null && IsWithinRuntimeDiagnosticsPath(normalizedDestination, normalizedWorkspace))
                return PermissionDecision.Deny(PermissionReasonCode.ProtectedRuntimeDiagnosticsPathDenied, "Mutation denied for protected runtime diagnostics path", normalizedDestination, normalizedWorkspace);
        }

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
            {
                if (!CommandRiskPolicy.HasExplicitApprovalMarker(action.Payload))
                    return CreateApprovalRequired(action, PermissionReasonCode.WriteModeDeleteDenied, "Destructive operation requires explicit approval in WorkspaceWrite mode", normalizedTarget ?? normalizedSource ?? normalizedDestination ?? normalizedWorkspace, normalizedWorkspace, "high");
            }
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

    private bool IsWithinExecutionWorkspace(string path, AgentSessionContext session)
    {
        var executionRoot = session.ExecutionWorkspaceRoot ?? session.ActiveWorkspaceRoot;
        if (string.IsNullOrWhiteSpace(executionRoot))
            return false;

        var normalizedExecutionRoot = _paths.Normalize(executionRoot);
        return IsWithinWorkspace(path, normalizedExecutionRoot);
    }

    private static bool IsWriteLike(ToolActionKind kind) =>
        kind is ToolActionKind.WriteFile or ToolActionKind.CreateFile or ToolActionKind.PatchFile;

    private static bool IsDeleteLike(ToolActionKind kind) => kind == ToolActionKind.DeleteFile;
    private static bool IsRenameLike(ToolActionKind kind) => kind == ToolActionKind.RenameFile;
    private static bool IsMoveLike(ToolActionKind kind) => kind == ToolActionKind.MoveFile;
    private static bool IsRuntimeDiagnosticsMutation(ToolActionKind kind) => IsWriteLike(kind) || IsDeleteLike(kind) || IsRenameLike(kind) || IsMoveLike(kind);
    private static bool IsWithinRuntimeDiagnosticsPath(string path, string workspaceRoot) =>
        IsWithinWorkspace(path, Path.Combine(workspaceRoot, ".agent-runtime"));

    private static PermissionDecision CreateApprovalRequired(ToolAction action, PermissionReasonCode code, string message, string normalizedTarget, string normalizedWorkspace, string riskLevel = "high")
    {
        var proposalId = ComputeProposalId(action, code, normalizedTarget, normalizedWorkspace);
        var proposal = new ActionApprovalProposal
        {
            ProposalId = proposalId,
            ActionType = action.Kind.ToString(),
            Command = action.Kind == ToolActionKind.RunCommand ? action.Payload : null,
            Path = action.TargetPath ?? action.SourcePath ?? action.DestinationPath ?? action.WorkingDirectory,
            NormalizedTarget = normalizedTarget,
            SandboxRoot = normalizedWorkspace,
            ProjectRoot = normalizedWorkspace,
            WorktreeRoot = normalizedWorkspace,
            IsInsideSandbox = false,
            RiskLevel = riskLevel,
            ReasonCode = PermissionDecision.ToReasonCodeString(code),
            ExpectedEffect = BuildExpectedEffect(action),
            Reason = message,
            RequiresApproval = true,
            ApprovalStatus = ApprovalStatus.ApprovalRequired
        };

        return PermissionDecision.ApprovalRequired(
            code,
            message,
            proposal,
            normalizedTarget,
            normalizedWorkspace);
    }

    private static string ComputeProposalId(ToolAction action, PermissionReasonCode code, string normalizedTarget, string normalizedWorkspace)
    {
        var signature = string.Join("|", new[]
        {
            action.Kind.ToString(),
            normalizedTarget,
            normalizedWorkspace,
            code.ToString(),
            action.Payload ?? string.Empty
        });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(signature)))
            .ToLowerInvariant()[..16];
    }

    private static string BuildExpectedEffect(ToolAction action)
    {
        return action.Kind switch
        {
            ToolActionKind.ReadFile => "Reads file content from the specified target path.",
            ToolActionKind.WriteFile => "Writes content to the specified target path.",
            ToolActionKind.CreateFile => "Creates a new file at the specified target path.",
            ToolActionKind.DeleteFile => "Deletes the specified file path.",
            ToolActionKind.MoveFile => "Moves a file from source path to destination path.",
            ToolActionKind.RenameFile => "Renames a file from source path to destination path.",
            ToolActionKind.PatchFile => "Applies in-place modifications to the target file.",
            ToolActionKind.RunCommand => "Executes the requested command in the specified working directory.",
            ToolActionKind.Build => "Runs project build command in the specified working directory.",
            ToolActionKind.Test => "Runs project tests in the specified working directory.",
            _ => "Performs the requested tool action on the specified target."
        };
    }
}
