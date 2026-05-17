namespace LocalCursorAgent.Security;

public sealed class PermissionGuard
{
    private readonly PathNormalizer _paths = new();

    public PermissionDecision Evaluate(AgentSessionContext session, ToolAction action)
    {
        var capabilityAssessment = CapabilityTierClassifier.Classify(action);
        PermissionDecision WithCapability(PermissionDecision decision) => decision.WithCapabilityMetadata(
            capabilityAssessment.CapabilityClass,
            capabilityAssessment.CapabilityTier,
            capabilityAssessment.Gate,
            capabilityAssessment.CommandPolicyCategory);

        var workspaceRoot = session.ExecutionWorkspaceRoot ?? session.ActiveWorkspaceRoot;
        if (string.IsNullOrWhiteSpace(session.RuntimeRoot) || string.IsNullOrWhiteSpace(workspaceRoot))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.WorkspaceNotResolved, "Workspace root not resolved"));

        if (PathSafetyPolicy.HasExtendedLengthPrefix(action.TargetPath))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ExtendedLengthPathDenied, "Target uses extended-length path prefix", action.TargetPath, workspaceRoot));

        if (PathSafetyPolicy.HasExtendedLengthPrefix(action.SourcePath))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ExtendedLengthPathDenied, "Source uses extended-length path prefix", action.SourcePath, session.ActiveWorkspaceRoot));

        if (PathSafetyPolicy.HasExtendedLengthPrefix(action.DestinationPath))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ExtendedLengthPathDenied, "Destination uses extended-length path prefix", action.DestinationPath, session.ActiveWorkspaceRoot));

        if (PathSafetyPolicy.HasExtendedLengthPrefix(action.WorkingDirectory))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ExtendedLengthPathDenied, "Working directory uses extended-length path prefix", action.WorkingDirectory, session.ActiveWorkspaceRoot));

        if (PathSafetyPolicy.HasRelativeDriveSyntax(action.TargetPath))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.InvalidPathSyntaxDenied, "Target uses drive-relative syntax", action.TargetPath, session.ActiveWorkspaceRoot));

        if (PathSafetyPolicy.HasRelativeDriveSyntax(action.SourcePath))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.InvalidPathSyntaxDenied, "Source uses drive-relative syntax", action.SourcePath, session.ActiveWorkspaceRoot));

        if (PathSafetyPolicy.HasRelativeDriveSyntax(action.DestinationPath))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.InvalidPathSyntaxDenied, "Destination uses drive-relative syntax", action.DestinationPath, session.ActiveWorkspaceRoot));

        if (PathSafetyPolicy.HasRelativeDriveSyntax(action.WorkingDirectory))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.InvalidPathSyntaxDenied, "Working directory uses drive-relative syntax", action.WorkingDirectory, session.ActiveWorkspaceRoot));

        if (PathSafetyPolicy.HasAlternateDataStreamSyntax(action.TargetPath))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.AlternateDataStreamDenied, "Target uses alternate data stream syntax", action.TargetPath, session.ActiveWorkspaceRoot));

        if (PathSafetyPolicy.HasAlternateDataStreamSyntax(action.SourcePath))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.AlternateDataStreamDenied, "Source uses alternate data stream syntax", action.SourcePath, session.ActiveWorkspaceRoot));

        if (PathSafetyPolicy.HasAlternateDataStreamSyntax(action.DestinationPath))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.AlternateDataStreamDenied, "Destination uses alternate data stream syntax", action.DestinationPath, session.ActiveWorkspaceRoot));

        if (PathSafetyPolicy.HasAlternateDataStreamSyntax(action.WorkingDirectory))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.AlternateDataStreamDenied, "Working directory uses alternate data stream syntax", action.WorkingDirectory, session.ActiveWorkspaceRoot));

        string normalizedWorkspace;
        string? normalizedTarget = null;
        string? normalizedSource = null;
        string? normalizedDestination = null;
        string normalizedRuntimeRoot;

        try
        {
            normalizedWorkspace = _paths.Normalize(workspaceRoot);
            normalizedRuntimeRoot = _paths.Normalize(session.RuntimeRoot);

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
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.PathNormalizationFailed, "Path normalization failed"));
        }

        if (action.Kind is ToolActionKind.ReadFile or ToolActionKind.ListDirectory or ToolActionKind.SearchFiles)
        {
            var isListDirectory = action.Kind == ToolActionKind.ListDirectory;
            var isSearchFiles = action.Kind == ToolActionKind.SearchFiles;
            if (string.IsNullOrWhiteSpace(normalizedTarget))
            {
                var missingTargetReason = isSearchFiles
                    ? "workspace_search_target_unavailable"
                    : isListDirectory
                    ? "workspace_list_target_unavailable"
                    : "workspace_read_target_unavailable";
                return WithCapability(PermissionDecision.Deny(PermissionReasonCode.PathNormalizationFailed, missingTargetReason));
            }

            if (IsRuntimeStatePathDenied(normalizedTarget, normalizedWorkspace, normalizedRuntimeRoot))
            {
                var runtimeStateReason = isSearchFiles
                    ? "runtime_state_search_denied"
                    : isListDirectory
                    ? "runtime_state_list_denied"
                    : "runtime_state_read_denied";
                return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ToolDeniedByPolicy, runtimeStateReason, normalizedTarget, normalizedWorkspace));
            }

            var workspacePathPolicy = new WorkspacePathPolicy();
            var workspaceRoots = new WorkspacePathPolicyRoots
            {
                ApprovedWorkspaceRoot = normalizedWorkspace,
                RuntimeRoot = normalizedRuntimeRoot,
                ScratchRoot = Path.Combine(normalizedWorkspace, ".scratch"),
                ArtifactOutputRoot = Path.Combine(normalizedWorkspace, ".artifacts")
            };
            var workspaceDecision = workspacePathPolicy.Evaluate(
                workspaceRoots,
                WorkspaceRootKind.ApprovedWorkspace,
                (isListDirectory || isSearchFiles) ? WorkspacePathOperationKind.List : WorkspacePathOperationKind.Read,
                normalizedTarget);

            if (workspaceDecision.Decision != WorkspacePathDecisionKind.Allowed)
            {
                var mappedReason = MapWorkspacePathPolicyDeniedReason(workspaceDecision.ReasonCode);
                var deniedPrefix = isSearchFiles
                    ? "workspace_search_denied"
                    : isListDirectory
                    ? "workspace_list_denied"
                    : "workspace_read_denied";
                return WithCapability(PermissionDecision.Deny(mappedReason, $"{deniedPrefix}:{workspaceDecision.ReasonCode}", normalizedTarget, normalizedWorkspace));
            }

            return WithCapability(PermissionDecision.Allow(normalizedTarget, normalizedWorkspace));
        }

        if (normalizedTarget is not null && PathSafetyPolicy.IsUncPath(normalizedTarget))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.NetworkPathDenied, "Target is a network path", normalizedTarget, normalizedWorkspace));

        if (normalizedSource is not null && PathSafetyPolicy.IsUncPath(normalizedSource))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.NetworkPathDenied, "Source is a network path", normalizedSource, normalizedWorkspace));

        if (normalizedDestination is not null && PathSafetyPolicy.IsUncPath(normalizedDestination))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.NetworkPathDenied, "Destination is a network path", normalizedDestination, normalizedWorkspace));

        if (normalizedTarget is not null && !IsWithinCanonicalWorkspace(normalizedTarget, normalizedWorkspace))
            return WithCapability(CreateApprovalRequired(session, action, PermissionReasonCode.PathOutsideWorkspace, "Target is outside active workspace", normalizedTarget, normalizedWorkspace));

        if (normalizedSource is not null && !IsWithinCanonicalWorkspace(normalizedSource, normalizedWorkspace))
            return WithCapability(CreateApprovalRequired(session, action, PermissionReasonCode.PathOutsideWorkspace, "Source is outside active workspace", normalizedSource, normalizedWorkspace));

        if (normalizedDestination is not null && !IsWithinCanonicalWorkspace(normalizedDestination, normalizedWorkspace))
            return WithCapability(CreateApprovalRequired(session, action, PermissionReasonCode.PathOutsideWorkspace, "Destination is outside active workspace", normalizedDestination, normalizedWorkspace));

        if (action.Kind == ToolActionKind.RunCommand)
        {
            var hasExplicitExecutable = !string.IsNullOrWhiteSpace(action.CommandExecutable);
            var hasExplicitArgs = action.CommandArgs is { Count: > 0 };
            var policyInput = hasExplicitExecutable || hasExplicitArgs
                ? new CommandPolicyInput
                {
                    Executable = action.CommandExecutable,
                    Args = action.CommandArgs,
                    RawCommandText = action.Payload,
                    WorkingDirectory = action.WorkingDirectory,
                    CommandKind = action.Kind,
                    Source = "permission_guard"
                }
                : CommandRiskPolicy.BuildInputFromRawCommand(action.Payload, action.WorkingDirectory, action.Kind, "permission_guard");
            var commandDecision = CommandRiskPolicy.Evaluate(policyInput);
            var commandTarget = normalizedTarget ?? normalizedWorkspace;
            switch (commandDecision.Category)
            {
                case CommandPolicyCategory.Allowed:
                    return WithCapability(PermissionDecision.Allow(commandTarget, normalizedWorkspace));

                case CommandPolicyCategory.HighRiskApprovalRequired:
                {
                    if (!session.IsApprovalLedgerHealthy)
                        return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ApprovalStateUnavailable, $"Approval state unavailable: {session.ApprovalLedgerError}"));
                    var highRiskValidation = ValidateBoundApprovalTokenForAction(session, action, PermissionReasonCode.HighRiskApprovalRequired, commandTarget, normalizedWorkspace);
                    if (!highRiskValidation.Allowed)
                    {
                        if (CommandRiskPolicy.HasExplicitApprovalMarker(action.Payload))
                            return WithCapability(PermissionDecision.Deny(highRiskValidation.ReasonCode, highRiskValidation.Message, commandTarget, normalizedWorkspace));
                        return WithCapability(CreateApprovalRequired(session, action, PermissionReasonCode.HighRiskApprovalRequired, "High-risk host/system/network-impacting command requires explicit approval", commandTarget, normalizedWorkspace, commandDecision.RiskLevel));
                    }

                    return WithCapability(PermissionDecision.Allow(commandTarget, normalizedWorkspace));
                }

                case CommandPolicyCategory.UnsupportedShellMetaSyntax:
                    return WithCapability(PermissionDecision.Deny(PermissionReasonCode.CommandUnsupportedShellSyntax, "Command contains unsupported shell/meta syntax and cannot be approved.", commandTarget, normalizedWorkspace));

                case CommandPolicyCategory.HardBlocked:
                    return WithCapability(PermissionDecision.Deny(PermissionReasonCode.CommandHardBlocked, "Command is hard-blocked by command policy and cannot be approved.", commandTarget, normalizedWorkspace));

                case CommandPolicyCategory.InvalidMalformed:
                default:
                    return WithCapability(PermissionDecision.Deny(PermissionReasonCode.CommandMalformed, "Command is malformed or missing executable/arguments.", commandTarget, normalizedWorkspace));
            }
        }

        if (normalizedTarget is not null && session.ProtectedPathPolicy.IsProtected(normalizedTarget) && !IsWithinExecutionWorkspace(normalizedTarget, session))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ProtectedPathDenied, "Target is protected", normalizedTarget, normalizedWorkspace));

        if (normalizedSource is not null && session.ProtectedPathPolicy.IsProtected(normalizedSource) && !IsWithinExecutionWorkspace(normalizedSource, session))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ProtectedPathDenied, "Source is protected", normalizedSource, normalizedWorkspace));

        if (normalizedDestination is not null && session.ProtectedPathPolicy.IsProtected(normalizedDestination) && !IsWithinExecutionWorkspace(normalizedDestination, session))
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ProtectedPathDenied, "Destination is protected", normalizedDestination, normalizedWorkspace));

        if (action.Kind is not ToolActionKind.ReadFile and not ToolActionKind.ListDirectory and not ToolActionKind.SearchFiles &&
            normalizedTarget is not null &&
            PathSafetyPolicy.ContainsReparsePoint(normalizedTarget))
        {
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ReparsePointDenied, "Target path contains a reparse point", normalizedTarget, normalizedWorkspace));
        }

        if (action.Kind is not ToolActionKind.ReadFile and not ToolActionKind.ListDirectory and not ToolActionKind.SearchFiles &&
            normalizedSource is not null &&
            PathSafetyPolicy.ContainsReparsePoint(normalizedSource))
        {
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ReparsePointDenied, "Source path contains a reparse point", normalizedSource, normalizedWorkspace));
        }

        if (action.Kind is not ToolActionKind.ReadFile and not ToolActionKind.ListDirectory and not ToolActionKind.SearchFiles &&
            normalizedDestination is not null &&
            PathSafetyPolicy.ContainsReparsePoint(normalizedDestination))
        {
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ReparsePointDenied, "Destination path contains a reparse point", normalizedDestination, normalizedWorkspace));
        }

        if (IsRuntimeDiagnosticsMutation(action.Kind))
        {
            if (normalizedTarget is not null && IsWithinRuntimeDiagnosticsPath(normalizedTarget, normalizedWorkspace))
                return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ProtectedRuntimeDiagnosticsPathDenied, "Mutation denied for protected runtime diagnostics path", normalizedTarget, normalizedWorkspace));

            if (normalizedSource is not null && IsWithinRuntimeDiagnosticsPath(normalizedSource, normalizedWorkspace))
                return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ProtectedRuntimeDiagnosticsPathDenied, "Mutation denied for protected runtime diagnostics path", normalizedSource, normalizedWorkspace));

            if (normalizedDestination is not null && IsWithinRuntimeDiagnosticsPath(normalizedDestination, normalizedWorkspace))
                return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ProtectedRuntimeDiagnosticsPathDenied, "Mutation denied for protected runtime diagnostics path", normalizedDestination, normalizedWorkspace));
        }

        if (session.AccessMode == AgentAccessMode.ReadOnly)
        {
            if (IsWriteLike(action.Kind))
                return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ReadOnlyWriteDenied, "Write denied in ReadOnly mode", normalizedTarget, normalizedWorkspace));

            if (IsDeleteLike(action.Kind))
                return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ReadOnlyDeleteDenied, "Delete denied in ReadOnly mode", normalizedTarget, normalizedWorkspace));

            if (IsRenameLike(action.Kind) || IsMoveLike(action.Kind))
                return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ReadOnlyWriteDenied, "Rename/move denied in ReadOnly mode", normalizedTarget, normalizedWorkspace));
        }

        if (session.AccessMode == AgentAccessMode.WorkspaceWrite)
        {
            if (IsDeleteLike(action.Kind) || IsRenameLike(action.Kind) || IsMoveLike(action.Kind))
            {
                if (!session.IsApprovalLedgerHealthy)
                    return WithCapability(PermissionDecision.Deny(PermissionReasonCode.ApprovalStateUnavailable, $"Approval state unavailable: {session.ApprovalLedgerError}"));
                var target = normalizedTarget ?? normalizedSource ?? normalizedDestination ?? normalizedWorkspace;
                if (!CommandRiskPolicy.HasExplicitApprovalMarker(action.Payload))
                    return WithCapability(CreateApprovalRequired(session, action, PermissionReasonCode.WriteModeDeleteDenied, "Destructive operation requires explicit approval in WorkspaceWrite mode", normalizedTarget ?? normalizedSource ?? normalizedDestination ?? normalizedWorkspace, normalizedWorkspace, "high"));
                var destructiveValidation = ValidateBoundApprovalTokenForAction(session, action, PermissionReasonCode.WriteModeDeleteDenied, target, normalizedWorkspace);
                if (!destructiveValidation.Allowed)
                    return WithCapability(PermissionDecision.Deny(destructiveValidation.ReasonCode, destructiveValidation.Message, target, normalizedWorkspace));
            }
        }

        if ((action.Kind == ToolActionKind.Build || action.Kind == ToolActionKind.Test) &&
            !string.IsNullOrWhiteSpace(normalizedTarget) &&
            !Directory.Exists(normalizedTarget))
        {
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.InvalidWorkingDirectory, "Working directory does not exist", normalizedTarget, normalizedWorkspace));
        }

        if ((action.Kind == ToolActionKind.RenameFile || action.Kind == ToolActionKind.MoveFile) &&
            string.IsNullOrWhiteSpace(normalizedSource))
        {
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.PathNormalizationFailed, "Source path is required for rename/move"));
        }

        if ((action.Kind == ToolActionKind.RenameFile || action.Kind == ToolActionKind.MoveFile) &&
            string.IsNullOrWhiteSpace(normalizedDestination))
        {
            return WithCapability(PermissionDecision.Deny(PermissionReasonCode.PathNormalizationFailed, "Destination path is required for rename/move"));
        }

        var resolvedTarget = normalizedTarget ?? normalizedSource ?? normalizedDestination;
        return WithCapability(PermissionDecision.Allow(resolvedTarget, normalizedWorkspace));
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

    private static bool IsRuntimeStatePathDenied(string normalizedTarget, string normalizedWorkspace, string normalizedRuntimeRoot)
    {
        var workspaceRuntimeDiagnostics = Path.TrimEndingDirectorySeparator(Path.Combine(normalizedWorkspace, ".agent-runtime"));
        if (IsWithinWorkspace(normalizedTarget, normalizedWorkspace))
            return IsWithinWorkspace(normalizedTarget, workspaceRuntimeDiagnostics);

        return IsWithinWorkspace(normalizedTarget, normalizedRuntimeRoot);
    }

    private static PermissionReasonCode MapWorkspacePathPolicyDeniedReason(string reasonCode) => reasonCode switch
    {
        "path_outside_root" => PermissionReasonCode.PathOutsideWorkspace,
        "reparse_escape_denied" => PermissionReasonCode.PathOutsideWorkspace,
        "unc_path_denied" => PermissionReasonCode.NetworkPathDenied,
        "extended_path_denied" => PermissionReasonCode.ExtendedLengthPathDenied,
        "alternate_data_stream_denied" => PermissionReasonCode.AlternateDataStreamDenied,
        "drive_relative_path_denied" => PermissionReasonCode.InvalidPathSyntaxDenied,
        "reparse_state_unavailable" => PermissionReasonCode.ReparsePointDenied,
        "reparse_resolution_unavailable" => PermissionReasonCode.ReparsePointDenied,
        "root_state_unavailable" => PermissionReasonCode.PathNormalizationFailed,
        "path_state_unavailable" => PermissionReasonCode.PathNormalizationFailed,
        "path_parent_unavailable" => PermissionReasonCode.PathNormalizationFailed,
        "requested_path_empty" => PermissionReasonCode.PathNormalizationFailed,
        "path_normalization_failed" => PermissionReasonCode.PathNormalizationFailed,
        _ => PermissionReasonCode.PathNormalizationFailed
    };

    private static PermissionDecision CreateApprovalRequired(AgentSessionContext session, ToolAction action, PermissionReasonCode code, string message, string normalizedTarget, string normalizedWorkspace, string riskLevel = "high")
    {
        var capabilityAssessment = CapabilityTierClassifier.Classify(action);
        var proposalFingerprint = CapabilityFingerprintV1.FromAssessment(action, capabilityAssessment);
        var issuedAtUtc = session.UtcNowProvider();
        var ttlSeconds = AgentSessionContext.ApprovalTokenTtlSecondsDefault;
        var expiresAtUtc = issuedAtUtc.AddSeconds(ttlSeconds);
        var proposalId = ComputeProposalId(session, action, code, normalizedTarget, normalizedWorkspace, proposalFingerprint);
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
            SessionBound = true,
            CapabilityFingerprint = proposalFingerprint
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

    private static string ComputeProposalId(AgentSessionContext session, ToolAction action, PermissionReasonCode code, string normalizedTarget, string normalizedWorkspace, CapabilityFingerprintV1? capabilityFingerprint) =>
        ApprovalProposalIdentityV1.ComputeProposalId(
            session.SessionId,
            action,
            code,
            normalizedTarget,
            normalizedWorkspace,
            capabilityFingerprint);

    private static ApprovalValidationResult ValidateBoundApprovalTokenForAction(AgentSessionContext session, ToolAction action, PermissionReasonCode code, string normalizedTarget, string normalizedWorkspace)
    {
        if (!CommandRiskPolicy.TryExtractApprovalToken(action.Payload, out var token))
            return ApprovalValidationResult.Denied(code, "Approval token is required.");

        var sanitizedAction = SanitizeActionForIdentity(action);
        if (!TryBuildCapabilityFingerprint(sanitizedAction, out var currentFingerprint) || currentFingerprint is null)
            return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalCapabilityBindingUnavailable, "Approval capability binding unavailable.");

        var expected = ComputeProposalId(session, sanitizedAction, code, normalizedTarget, normalizedWorkspace, currentFingerprint);
        var resolvedProposalId = expected;
        if (!token.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryResolveLegacyProposalIdForValidation(session, sanitizedAction, code, normalizedTarget, normalizedWorkspace, token, out resolvedProposalId))
            {
                if (!session.RecordApprovalDeniedEvent(expected, "denied_invalid_token", PermissionDecision.ToReasonCodeString(code), action.RunId))
                    return ApprovalValidationResult.LedgerUnavailable(session);
                return ApprovalValidationResult.Denied(code, "Approval token mismatch.");
            }
        }
        if (session.IsApprovalProposalConsumed(resolvedProposalId))
        {
            if (!session.RecordApprovalDeniedEvent(resolvedProposalId, "denied_consumed", PermissionReasonCodes.ApprovalTokenExpired, action.RunId))
                return ApprovalValidationResult.LedgerUnavailable(session);
            return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalTokenExpired, "Approval token expired.");
        }
        if (session.IsApprovalProposalExpired(resolvedProposalId))
        {
            if (!session.RecordApprovalDeniedEvent(resolvedProposalId, "denied_expired", PermissionReasonCodes.ApprovalTokenExpired, action.RunId))
                return ApprovalValidationResult.LedgerUnavailable(session);
            return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalTokenExpired, "Approval token expired.");
        }
        var proposal = session.GetApprovalProposal(resolvedProposalId);
        if (proposal is null)
        {
            if (!session.RecordApprovalDeniedEvent(resolvedProposalId, "denied_invalid_token", PermissionDecision.ToReasonCodeString(code), action.RunId))
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
                if (!session.RecordApprovalDeniedEvent(resolvedProposalId, "denied_run_binding_unavailable", PermissionReasonCodes.ApprovalRunBindingUnavailable, proposalRunId))
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
                    if (!session.RecordApprovalDeniedEvent(resolvedProposalId, "denied_run_binding_unavailable", PermissionReasonCodes.ApprovalRunBindingUnavailable, currentRunId))
                        return ApprovalValidationResult.LedgerUnavailable(session);
                    return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalRunBindingUnavailable, "Approval run binding unavailable.");
                }
            }
            else if (!currentRunId.Equals(proposalRunId, StringComparison.OrdinalIgnoreCase))
            {
                if (!session.RecordApprovalDeniedEvent(resolvedProposalId, "denied_run_mismatch", PermissionReasonCodes.ApprovalRunMismatch, currentRunId))
                    return ApprovalValidationResult.LedgerUnavailable(session);
                return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalRunMismatch, "Approval is bound to a different execution attempt.");
            }
        }

        var proposalFingerprint = proposal.CapabilityFingerprint;
        var legacyFingerprintless = IsLegacyFingerprintlessProposal(proposal);
        if (proposalFingerprint is null && !legacyFingerprintless)
            return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalCapabilityBindingUnavailable, "Approval capability binding unavailable.");
        if (proposalFingerprint is not null &&
            !CapabilityFingerprintsEqual(currentFingerprint, proposalFingerprint))
        {
            if (!session.RecordApprovalDeniedEvent(resolvedProposalId, "denied_capability_mismatch", PermissionReasonCodes.ApprovalCapabilityMismatch, proposalRunId ?? currentRunId))
                return ApprovalValidationResult.LedgerUnavailable(session);
            return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalCapabilityMismatch, "Approval is bound to a different capability profile.");
        }

        if (session.UtcNowProvider() > proposal.ExpiresAtUtc)
        {
            if (!session.MarkApprovalProposalExpired(resolvedProposalId, PermissionReasonCodes.ApprovalTokenExpired, proposalRunId ?? currentRunId))
                return ApprovalValidationResult.LedgerUnavailable(session);
            if (!session.RecordApprovalDeniedEvent(resolvedProposalId, "denied_expired", PermissionReasonCodes.ApprovalTokenExpired, proposalRunId ?? currentRunId))
                return ApprovalValidationResult.LedgerUnavailable(session);
            return ApprovalValidationResult.Denied(PermissionReasonCode.ApprovalTokenExpired, "Approval token expired.");
        }
        return ApprovalValidationResult.Valid();
    }

    private static bool TryResolveLegacyProposalIdForValidation(
        AgentSessionContext session,
        ToolAction sanitizedAction,
        PermissionReasonCode code,
        string normalizedTarget,
        string normalizedWorkspace,
        string token,
        out string resolvedProposalId)
    {
        resolvedProposalId = string.Empty;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var proposal = session.GetApprovalProposal(token);
        if (proposal is null)
            return false;
        if (!string.Equals(token, proposal.ProposalId, StringComparison.Ordinal))
            return false;
        if (ApprovalProposalIdentityV1.IsCanonicalProposalId(proposal.ProposalId))
            return false;
        if (proposal.IssuedAtUtc >= ApprovalProposalIdentityV1.CutoverUtc)
            return false;

        var legacyExpected = ComputeLegacyProposalId(session, sanitizedAction, code, normalizedTarget, normalizedWorkspace);
        if (!legacyExpected.Equals(proposal.ProposalId, StringComparison.OrdinalIgnoreCase))
            return false;

        resolvedProposalId = proposal.ProposalId;
        return true;
    }

    private static string ComputeLegacyProposalId(AgentSessionContext session, ToolAction action, PermissionReasonCode code, string normalizedTarget, string normalizedWorkspace)
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

    private static ToolAction SanitizeActionForIdentity(ToolAction action)
    {
        var strippedPayload = StripApprovalToken(action.Payload);
        var strippedCommandArgs = StripApprovalTokenFromArgs(action.CommandArgs);

        return new ToolAction
        {
            Kind = action.Kind,
            RunId = action.RunId,
            TargetPath = action.TargetPath,
            SourcePath = action.SourcePath,
            DestinationPath = action.DestinationPath,
            WorkingDirectory = action.WorkingDirectory,
            CommandExecutable = action.CommandExecutable,
            CommandArgs = strippedCommandArgs,
            Payload = strippedPayload
        };
    }

    private static IReadOnlyList<string>? StripApprovalTokenFromArgs(IReadOnlyList<string>? args)
    {
        if (args is null || args.Count == 0)
            return args;

        var output = new List<string>(args.Count);
        foreach (var arg in args)
        {
            var sanitized = StripApprovalToken(arg);
            if (string.IsNullOrWhiteSpace(sanitized))
                continue;
            output.Add(sanitized);
        }

        return output.ToArray();
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
            ToolActionKind.ListDirectory => "Lists directory entries from the specified target path.",
            ToolActionKind.SearchFiles => "Searches file names under the specified target path.",
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

    private static bool TryBuildCapabilityFingerprint(ToolAction action, out CapabilityFingerprintV1? fingerprint)
    {
        try
        {
            var capabilityAssessment = CapabilityTierClassifier.Classify(action);
            fingerprint = CapabilityFingerprintV1.FromAssessment(action, capabilityAssessment);
            return true;
        }
        catch
        {
            fingerprint = null;
            return false;
        }
    }

    private static bool IsLegacyFingerprintlessProposal(ActionApprovalProposal proposal)
    {
        if (proposal.CapabilityFingerprint is not null)
            return false;

        return proposal.IssuedAtUtc < AgentSessionContext.CapabilityFingerprintCutoverUtc;
    }

    private static bool CapabilityFingerprintsEqual(CapabilityFingerprintV1 current, CapabilityFingerprintV1 proposal)
    {
        return current.FingerprintVersion == proposal.FingerprintVersion &&
               current.ActionKind.Equals(proposal.ActionKind, StringComparison.Ordinal) &&
               current.CapabilityClass.Equals(proposal.CapabilityClass, StringComparison.Ordinal) &&
               current.CapabilityTier == proposal.CapabilityTier &&
               current.CapabilityGate.Equals(proposal.CapabilityGate, StringComparison.Ordinal) &&
               NormalizeOptionalValue(current.PolicyCategory).Equals(NormalizeOptionalValue(proposal.PolicyCategory), StringComparison.Ordinal) &&
               current.ActionProfile.Equals(proposal.ActionProfile, StringComparison.Ordinal);
    }

    private static string NormalizeOptionalValue(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized;
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
