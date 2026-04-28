using System.Security.Cryptography;
using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Tools;

namespace LocalCursorAgent.Security;

public sealed class PatchSafetyGate
{
    private readonly AgentSessionContext _session;
    private readonly PermissionGuard _permissionGuard;
    private readonly TextFileService _textFileService;
    private readonly ExecutionTracer? _tracer;
    private readonly Func<PatchTraceRecord, bool>? _failureHook;

    public PatchSafetyGate(AgentSessionContext session, PermissionGuard permissionGuard, ExecutionTracer? tracer = null, Func<PatchTraceRecord, bool>? failureHook = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _permissionGuard = permissionGuard ?? throw new ArgumentNullException(nameof(permissionGuard));
        _textFileService = new TextFileService();
        _tracer = tracer;
        _failureHook = failureHook;
    }

    public PatchPreviewResult Preview(string targetPath, string? expectedTargetPath, string? patchText, string? anchorHint = null)
    {
        var resolvedTarget = ResolvePath(targetPath);
        Trace("PatchPreviewStarted", resolvedTarget, null, false, false, false, false, false, null, 1);
        if (ShouldForceFailure(resolvedTarget, "PatchPreviewStarted"))
        {
            Trace("PatchPreviewRejected", resolvedTarget, null, false, false, false, false, false, PermissionReasonCodes.PatchPreviewRejected, 2);
            return Reject(PermissionReasonCodes.PatchPreviewRejected, "Forced patch preview failure.", resolvedTarget);
        }
        if (string.IsNullOrWhiteSpace(resolvedTarget))
        {
            Trace("PatchPreviewRejected", targetPath, null, false, false, false, false, false, PermissionReasonCodes.PathNormalizationFailed, 2);
            return Reject(PermissionReasonCodes.PathNormalizationFailed, "Failed to normalize target path.", targetPath);
        }

        if (!string.IsNullOrWhiteSpace(expectedTargetPath))
        {
            var resolvedExpected = ResolvePath(expectedTargetPath);
            if (!PathEquals(resolvedTarget, resolvedExpected))
            {
                Trace("PatchPreviewRejected", resolvedTarget, null, false, false, false, false, false, PermissionReasonCodes.PatchTargetMismatch, 2);
                return Reject(PermissionReasonCodes.PatchTargetMismatch, "Resolved target does not match expected target.", resolvedTarget);
            }
        }

        if (!string.IsNullOrWhiteSpace(patchText))
        {
            var previewReason = ClassifyPatchText(patchText);
            if (!string.Equals(previewReason, PermissionReasonCodes.Allowed, StringComparison.Ordinal))
            {
                Trace("PatchPreviewRejected", resolvedTarget, null, false, false, false, false, false, previewReason, 2);
                return Reject(previewReason, "Patch text failed validation.", resolvedTarget);
            }
        }

        var targetExists = File.Exists(resolvedTarget) || Directory.Exists(resolvedTarget);
        if (!targetExists)
        {
            var createAction = new ToolAction
            {
                Kind = ToolActionKind.PatchFile,
                TargetPath = resolvedTarget
            };

            var createDecision = _permissionGuard.Evaluate(_session, createAction);
            if (!createDecision.Allowed)
            {
                Trace("PatchPreviewRejected", resolvedTarget, null, false, false, false, false, false, createDecision.ReasonCodeString, 2);
                return Reject(createDecision.ReasonCodeString, createDecision.Message, resolvedTarget);
            }

            Trace("PatchPreviewAccepted", resolvedTarget, string.Empty, true, false, false, false, false, PermissionReasonCodes.Allowed, 2);
            return new PatchPreviewResult
            {
                PreviewGenerated = true,
                AnchorFound = true,
                FileUnchangedSinceRead = true,
                ReasonCode = PermissionReasonCodes.Allowed,
                Message = "Preview accepted for new file creation.",
                TargetPath = resolvedTarget,
                SnapshotHashBeforeApply = string.Empty,
                ResolvedAnchor = anchorHint,
                PreviewText = patchText
            };
        }

        var originalHash = ComputePathHash(resolvedTarget);
        if (string.IsNullOrWhiteSpace(originalHash))
        {
            Trace("PatchPreviewRejected", resolvedTarget, null, false, false, false, false, false, PermissionReasonCodes.PatchPreviewRejected, 2);
            return Reject(PermissionReasonCodes.PatchPreviewRejected, "Unable to compute file hash.", resolvedTarget);
        }

        var anchorFound = string.IsNullOrWhiteSpace(anchorHint) || AnchorExists(resolvedTarget, anchorHint);
        if (!anchorFound)
        {
            Trace("PatchPreviewRejected", resolvedTarget, originalHash, false, false, false, false, false, PermissionReasonCodes.PatchAnchorNotFound, 2);
            return Reject(PermissionReasonCodes.PatchAnchorNotFound, "Patch anchor not found.", resolvedTarget, originalHash);
        }

        var fileUnchangedSinceRead = CheckUnchangedSinceRead(resolvedTarget, originalHash);
        if (!fileUnchangedSinceRead)
        {
            Trace("PatchPreviewRejected", resolvedTarget, originalHash, false, false, false, false, false, PermissionReasonCodes.PatchFileChangedSinceRead, 2);
            return Reject(PermissionReasonCodes.PatchFileChangedSinceRead, "Target file changed since it was read.", resolvedTarget, originalHash);
        }

        var action = new ToolAction
        {
            Kind = ToolActionKind.PatchFile,
            TargetPath = resolvedTarget
        };

        var decision = _permissionGuard.Evaluate(_session, action);
        if (!decision.Allowed)
        {
            Trace("PatchPreviewRejected", resolvedTarget, originalHash, false, false, false, false, false, decision.ReasonCodeString, 2);
            return Reject(decision.ReasonCodeString, decision.Message, resolvedTarget, originalHash);
        }

        Trace("PatchPreviewAccepted", resolvedTarget, originalHash, true, false, false, false, false, PermissionReasonCodes.Allowed, 2);
        return new PatchPreviewResult
        {
            PreviewGenerated = true,
            AnchorFound = anchorFound,
            FileUnchangedSinceRead = fileUnchangedSinceRead,
            ReasonCode = PermissionReasonCodes.Allowed,
            Message = "Preview accepted.",
            TargetPath = resolvedTarget,
            SnapshotHashBeforeApply = originalHash,
            ResolvedAnchor = anchorHint,
            PreviewText = patchText
        };
    }

    public async Task<PatchApplyResult> ApplyAsync(PatchPreviewResult preview, Func<Task> applyAction, Func<Task>? rollbackAction = null)
    {
        if (preview is null || !preview.PreviewGenerated || preview.PreviewRejected)
        {
            Trace("PatchPreviewRejected", preview?.TargetPath ?? string.Empty, preview?.SnapshotHashBeforeApply, false, false, false, false, false, PermissionReasonCodes.PatchPreviewRejected, 1);
            return new PatchApplyResult
            {
                ApplyFailed = true,
                RollbackFailed = false,
                ReasonCode = PermissionReasonCodes.PatchPreviewRejected,
                Message = "Patch preview was not accepted.",
                TargetPath = preview?.TargetPath ?? string.Empty
            };
        }

        var currentHash = ComputePathHash(preview.TargetPath);
        if (!string.Equals(currentHash, preview.SnapshotHashBeforeApply, StringComparison.OrdinalIgnoreCase))
        {
            Trace("PatchApplyStarted", preview.TargetPath, preview.SnapshotHashBeforeApply, true, false, false, false, false, PermissionReasonCodes.PatchFileChangedSinceRead, 1);
            Trace("PatchApplyFailed", preview.TargetPath, preview.SnapshotHashBeforeApply, true, false, true, false, false, PermissionReasonCodes.PatchFileChangedSinceRead, 2);
            return new PatchApplyResult
            {
                ApplyFailed = true,
                RollbackFailed = false,
                ReasonCode = PermissionReasonCodes.PatchFileChangedSinceRead,
                Message = "Target file changed since preview.",
                TargetPath = preview.TargetPath
            };
        }

        try
        {
            Trace("PatchApplyStarted", preview.TargetPath, preview.SnapshotHashBeforeApply, true, false, false, false, false, PermissionReasonCodes.Allowed, 1);
            if (ShouldForceFailure(preview.TargetPath, "PatchApplyStarted"))
                throw new InvalidOperationException("Forced patch apply failure.");
            await applyAction();
            Trace("PatchApplySucceeded", preview.TargetPath, preview.SnapshotHashBeforeApply, true, true, false, false, false, PermissionReasonCodes.Allowed, 2);
            return new PatchApplyResult
            {
                ApplySucceeded = true,
                ReasonCode = PermissionReasonCodes.Allowed,
                Message = "Patch applied successfully.",
                TargetPath = preview.TargetPath
            };
        }
        catch (Exception ex)
        {
            var reasonCode = ClassifyApplyException(ex);
            Trace("PatchApplyFailed", preview.TargetPath, preview.SnapshotHashBeforeApply, true, false, true, false, false, reasonCode, 2);
            var rollbackSucceeded = false;
            if (rollbackAction != null)
            {
                try
                {
                    Trace("PatchRollbackStarted", preview.TargetPath, preview.SnapshotHashBeforeApply, true, false, true, false, false, PermissionReasonCodes.PatchApplyFailed, 3);
                    await rollbackAction();
                    rollbackSucceeded = true;
                    Trace("PatchRollbackSucceeded", preview.TargetPath, preview.SnapshotHashBeforeApply, true, false, true, true, false, PermissionReasonCodes.Allowed, 4);
                }
                catch
                {
                    rollbackSucceeded = false;
                    Trace("PatchRollbackFailed", preview.TargetPath, preview.SnapshotHashBeforeApply, true, false, true, false, true, PermissionReasonCodes.PatchRollbackFailed, 4);
                }
            }

            return new PatchApplyResult
            {
                ApplyFailed = true,
                RollbackSucceeded = rollbackSucceeded,
                RollbackFailed = !rollbackSucceeded,
                ReasonCode = rollbackSucceeded ? reasonCode : PermissionReasonCodes.PatchRollbackFailed,
                Message = ex.Message,
                TargetPath = preview.TargetPath
            };
        }
    }

    private static string ClassifyPatchText(string patchText)
    {
        var text = patchText.Trim();
        if (text.Length == 0)
            return PermissionReasonCodes.PatchInvalidFormat;

        var hasBegin = text.Contains("*** Begin Patch", StringComparison.Ordinal);
        var hasEnd = text.Contains("*** End Patch", StringComparison.Ordinal);
        if (hasBegin && !hasEnd)
            return PermissionReasonCodes.PatchUnexpectedEndOfPatch;
        if (hasEnd && !hasBegin)
            return PermissionReasonCodes.PatchInvalidFormat;

        return PermissionReasonCodes.Allowed;
    }

    private static string ClassifyApplyException(Exception ex)
    {
        var msg = ex.Message ?? string.Empty;
        if (msg.IndexOf("context", StringComparison.OrdinalIgnoreCase) >= 0 &&
            msg.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0)
            return PermissionReasonCodes.PatchContextNotFound;
        if (msg.IndexOf("ambiguous", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("multiple matches", StringComparison.OrdinalIgnoreCase) >= 0)
            return PermissionReasonCodes.PatchAmbiguousMatch;
        if (msg.IndexOf("hunk", StringComparison.OrdinalIgnoreCase) >= 0 &&
            (msg.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 || msg.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0))
            return PermissionReasonCodes.PatchInvalidHunk;
        if (msg.IndexOf("unexpected end", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("eof", StringComparison.OrdinalIgnoreCase) >= 0)
            return PermissionReasonCodes.PatchUnexpectedEndOfPatch;
        if (msg.IndexOf("format", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("parse", StringComparison.OrdinalIgnoreCase) >= 0)
            return PermissionReasonCodes.PatchInvalidFormat;

        return PermissionReasonCodes.PatchApplyFailed;
    }

    private PatchPreviewResult Reject(string reasonCode, string message, string targetPath, string hash = "")
    {
        return new PatchPreviewResult
        {
            PreviewGenerated = true,
            PreviewRejected = true,
            AnchorFound = false,
            FileUnchangedSinceRead = false,
            ReasonCode = reasonCode,
            Message = message,
            TargetPath = targetPath,
            SnapshotHashBeforeApply = hash
        };
    }

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var fullPath = Path.IsPathFullyQualified(path)
            ? path
            : Path.Combine(_session.ActiveWorkspaceRoot, path);

        return Path.GetFullPath(fullPath);
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static string ComputePathHash(string path)
    {
        if (File.Exists(path))
        {
            var bytes = File.ReadAllBytes(path);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        if (Directory.Exists(path))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join("|", Directory.GetFiles(path, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)));
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        return string.Empty;
    }

    private static bool AnchorExists(string targetPath, string anchorHint)
    {
        if (string.IsNullOrWhiteSpace(anchorHint) || !File.Exists(targetPath))
            return true;

        var content = File.ReadAllText(targetPath);
        return content.Contains(anchorHint, StringComparison.Ordinal);
    }

    private static bool CheckUnchangedSinceRead(string targetPath, string snapshotHash)
    {
        if (string.IsNullOrWhiteSpace(snapshotHash))
            return false;

        var currentHash = ComputePathHash(targetPath);
        return string.Equals(currentHash, snapshotHash, StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldForceFailure(string targetPath, string step)
    {
        if (_failureHook == null)
            return false;

        return _failureHook(new PatchTraceRecord
        {
            OperationKind = "Patch",
            Step = step,
            TargetPath = targetPath,
            TimestampUtc = DateTime.UtcNow
        });
    }

    private void Trace(string step, string targetPath, string? snapshotHash, bool previewAccepted, bool applySucceeded, bool applyFailed, bool rollbackSucceeded, bool rollbackFailed, string? reasonCode, int stepOrder)
    {
        _tracer?.LogPatchLifecycle(new PatchTraceRecord
        {
            OperationKind = "Patch",
            Step = step,
            TargetPath = targetPath,
            SnapshotHashBeforeApply = snapshotHash,
            PreviewAccepted = previewAccepted,
            ApplySucceeded = applySucceeded,
            ApplyFailed = applyFailed,
            RollbackSucceeded = rollbackSucceeded,
            RollbackFailed = rollbackFailed,
            ReasonCode = reasonCode,
            TimestampUtc = DateTime.UtcNow,
            StepOrder = stepOrder
        });
    }
}
