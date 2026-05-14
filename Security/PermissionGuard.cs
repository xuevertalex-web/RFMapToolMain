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

        if (normalizedTarget is not null && !IsWithinCanonicalWorkspace(normalizedTarget, normalizedWorkspace))
            return CreateApprovalRequired(session, action, PermissionReasonCode.PathOutsideWorkspace, "Target is outside active workspace", normalizedTarget, normalizedWorkspace);

        if (normalizedSource is not null && !IsWithinCanonicalWorkspace(normalizedSource, normalizedWorkspace))
            return CreateApprovalRequired(session, action, PermissionReasonCode.PathOutsideWorkspace, "Source is outside active workspace", normalizedSource, normalizedWorkspace);

        if (normalizedDestination is not null && !IsWithinCanonicalWorkspace(normalizedDestination, normalizedWorkspace))
            return CreateApprovalRequired(session, action, PermissionReasonCode.PathOutsideWorkspace, "Destination is outside active workspace", normalizedDestination, normalizedWorkspace);

        if (action.Kind == ToolActionKind.RunCommand)
        {
            if (CommandRiskPolicy.IsHighRiskCommand(action.Payload))
            {
                if (!session.IsApprovalLedgerHealthy)
                    return PermissionDecision.Deny(PermissionReasonCode.ApprovalStateUnavailable, $"Approval state unavailable: {session.ApprovalLedgerError}");
                var highRiskValidation = ValidateBoundApprovalTokenForAction(session, action, PermissionReasonCode.HighRiskApprovalRequired, normalizedTarget ?? normalizedWorkspace, normalizedWorkspace);
                if (!highRiskValidation.Allowed)
                {
                    if (CommandRiskPolicy.HasExplicitApprovalMarker(action.Payload))
                        return PermissionDecision.Deny(highRiskValidation.ReasonCode, highRiskValidation.Message, normalizedTarget ?? normalizedWorkspace, normalizedWorkspace);
                    return CreateApprovalRequired(session, action, PermissionReasonCode.HighRiskApprovalRequired, "High-risk host/system/network-impacting command requires explicit approval", normalizedTarget ?? normalizedWorkspace, normalizedWorkspace, CommandRiskPolicy.ResolveCommandRiskLevel(action.Payload));
                }
            }

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
                if (!session.IsApprovalLedgerHealthy)
                    return PermissionDecision.Deny(PermissionReasonCode.ApprovalStateUnavailable, $"Approval state unavailable: {session.ApprovalLedgerError}");
                var target = normalizedTarget ?? normalizedSource ?? normalizedDestination ?? normalizedWorkspace;
                if (!CommandRiskPolicy.HasExplicitApprovalMarker(action.Payload))
                    return CreateApprovalRequired(session, action, PermissionReasonCode.WriteModeDeleteDenied, "Destructive operation requires explicit approval in WorkspaceWrite mode", normalizedTarget ?? normalizedSource ?? normalizedDestination ?? normalizedWorkspace, normalizedWorkspace, "high");
                var destructiveValidation = ValidateBoundApprovalTokenForAction(session, action, PermissionReasonCode.WriteModeDeleteDenied, target, normalizedWorkspace);
                if (!destructiveValidation.Allowed)
                    return PermissionDecision.Deny(destructiveValidation.ReasonCode, destructiveValidation.Message, target, normalizedWorkspace);
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
    private static bool IsWithinCanonicalWorkspace(string path, string workspaceRoot) =>
        IsWithinWorkspace(path, workspaceRoot) && CanonicalPathPolicy.IsCanonicallyContained(workspaceRoot, path);

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

    private static PermissionDecision CreateApprovalRequired(AgentSessionContext session, ToolAction action, PermissionReasonCode code, string message, string normalizedTarget, string normalizedWorkspace, string riskLevel = "high")
    {
        var issuedAtUtc = session.UtcNowProvider();
        var ttlSeconds = AgentSessionContext.ApprovalTokenTtlSecondsDefault;
        var expiresAtUtc = issuedAtUtc.AddSeconds(ttlSeconds);
        var proposalId = ComputeProposalId(session, action, code, normalizedTarget, normalizedWorkspace);
        var existing = session.GetApprovalProposal(proposalId);
        if (existing is not null &&
            !session.IsApprovalProposalConsumed(proposalId) &&
            !session.IsApprovalProposalExpired(proposalId) &&
            issuedAtUtc <= existing.ExpiresAtUtc)
        {
            return PermissionDecision.ApprovalRequired(
                code,
                message,
                existing,
                normalizedTarget,
                normalizedWorkspace);
        }

        var proposal = new ActionApprovalProposal
        {
            ProposalId = proposalId,
            RunId = AgentSessionContext.CreateRunId(),
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
            ApprovalStatus = ApprovalStatus.ApprovalRequired,
            IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            TtlSeconds = ttlSeconds,
            SessionId = session.SessionId,
            SessionBound = true
        };
        if (!session.RegisterApprovalProposal(proposal))
            return PermissionDecision.Deny(PermissionReasonCode.ApprovalStateUnavailable, $"Approval state unavailable: {session.ApprovalLedgerError}", normalizedTarget, normalizedWorkspace);

        return PermissionDecision.ApprovalRequired(
            code,
            message,
            proposal,
            normalizedTarget,
            normalizedWorkspace);
    }

    private static string ComputeProposalId(AgentSessionContext session, ToolAction action, PermissionReasonCode code, string normalizedTarget, string normalizedWorkspace)
    {
        var signature = string.Join("|", new[]
        {
            session.SessionId,
            action.Kind.ToString(),
            normalizedTarget,
            normalizedWorkspace,
            code.ToString(),
            action.Payload ?? string.Empty
        });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(signature)))
            .ToLowerInvariant()[..16];
    }

    private static ApprovalValidationResult ValidateBoundApprovalTokenForAction(AgentSessionContext session, ToolAction action, PermissionReasonCode code, string normalizedTarget, string normalizedWorkspace)
    {
        if (!CommandRiskPolicy.TryExtractApprovalToken(action.Payload, out var token))
            return ApprovalValidationResult.Denied(code, "Approval token is required.");
        var expected = ComputeProposalId(session, new ToolAction
        {
            Kind = action.Kind,
            RunId = action.RunId,
            TargetPath = action.TargetPath,
            SourcePath = action.SourcePath,
            DestinationPath = action.DestinationPath,
            WorkingDirectory = action.WorkingDirectory,
            Payload = StripApprovalToken(action.Payload)
        }, code, normalizedTarget, normalizedWorkspace);
        if (!token.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            if (!session.RecordApprovalDeniedEvent(expected, "denied_invalid_token", PermissionDecision.ToReasonCodeString(code), action.RunId))
                return ApprovalValidationResult.LedgerUnavailable(session);
            return ApprovalValidationResult.Denied(code, "Approval token mismatch.");
        }
        if (session.IsApprovalProposalConsumed(expected))
        {
            if (!session.RecordApprovalDeniedEvent(expected, "denied_consumed", PermissionReasonCodes.ApprovalTokenExpired, action.RunId))
                return ApprovalValidationResult.LedgerUnavailable(session);
            return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalTokenExpired, "Approval token expired.");
        }
        if (session.IsApprovalProposalExpired(expected))
        {
            if (!session.RecordApprovalDeniedEvent(expected, "denied_expired", PermissionReasonCodes.ApprovalTokenExpired, action.RunId))
                return ApprovalValidationResult.LedgerUnavailable(session);
            return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalTokenExpired, "Approval token expired.");
        }
        var proposal = session.GetApprovalProposal(expected);
        if (proposal is null)
        {
            if (!session.RecordApprovalDeniedEvent(expected, "denied_invalid_token", PermissionDecision.ToReasonCodeString(code), action.RunId))
                return ApprovalValidationResult.LedgerUnavailable(session);
            return ApprovalValidationResult.Denied(code, "Approval token mismatch.");
        }
        var currentRunId = NormalizeRunId(action.RunId);
        var proposalRunId = NormalizeRunId(proposal.RunId);
        var legacyRunIdless = string.IsNullOrWhiteSpace(proposalRunId) && proposal.IssuedAtUtc < AgentSessionContext.RunIdCutoverUtc;
        if (string.IsNullOrWhiteSpace(currentRunId))
        {
            if (!legacyRunIdless)
            {
                if (!session.RecordApprovalDeniedEvent(expected, "denied_run_binding_unavailable", PermissionReasonCodes.ApprovalRunBindingUnavailable, proposalRunId))
                    return ApprovalValidationResult.LedgerUnavailable(session);
                return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalRunBindingUnavailable, "Approval run binding unavailable.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(proposalRunId))
            {
                if (!legacyRunIdless)
                {
                    if (!session.RecordApprovalDeniedEvent(expected, "denied_run_binding_unavailable", PermissionReasonCodes.ApprovalRunBindingUnavailable, currentRunId))
                        return ApprovalValidationResult.LedgerUnavailable(session);
                    return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalRunBindingUnavailable, "Approval run binding unavailable.");
                }
            }
            else if (!currentRunId.Equals(proposalRunId, StringComparison.OrdinalIgnoreCase))
            {
                if (!session.RecordApprovalDeniedEvent(expected, "denied_run_mismatch", PermissionReasonCodes.ApprovalRunMismatch, currentRunId))
                    return ApprovalValidationResult.LedgerUnavailable(session);
                return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalRunMismatch, "Approval is bound to a different execution attempt.");
            }
        }

        if (session.UtcNowProvider() > proposal.ExpiresAtUtc)
        {
            if (!session.MarkApprovalProposalExpired(expected, PermissionReasonCodes.ApprovalTokenExpired, proposalRunId ?? currentRunId))
                return ApprovalValidationResult.LedgerUnavailable(session);
            if (!session.RecordApprovalDeniedEvent(expected, "denied_expired", PermissionReasonCodes.ApprovalTokenExpired, proposalRunId ?? currentRunId))
                return ApprovalValidationResult.LedgerUnavailable(session);
            return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalTokenExpired, "Approval token expired.");
        }
        return ApprovalValidationResult.Valid();
    }

    private static string? StripApprovalToken(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return payload;
        const string marker = "APPROVED:";
        var idx = payload.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return payload;
        var tokenEnd = payload.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }, idx + marker.Length);
        return tokenEnd >= 0 ? payload.Remove(idx, tokenEnd - idx).Trim() : payload[..idx].Trim();
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

    private static string? NormalizeRunId(string? runId)
    {
        var normalized = runId?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private readonly record struct ApprovalValidationResult(bool Allowed, PermissionReasonCode ReasonCode, string Message)
    {
        public static ApprovalValidationResult Valid() => new(true, PermissionReasonCode.Allowed, "Allowed");
        public static ApprovalValidationResult Denied(PermissionReasonCode code, string message) => new(false, code, message);
        public static ApprovalValidationResult LedgerUnavailable(AgentSessionContext session) =>
            new(false, PermissionReasonCode.ApprovalStateUnavailable, $"Approval state unavailable: {session.ApprovalLedgerError}");
    }
}
