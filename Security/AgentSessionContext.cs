using System.Security.Cryptography;
using System.Text;

namespace LocalCursorAgent.Security;

public sealed class AgentSessionContext : IDisposable
{
    private const string UnknownStateUnavailable = "unknown_state_unavailable";
    private static readonly HashSet<string> KnownApprovalLedgerErrorCodes = new(StringComparer.Ordinal)
    {
        "owner_lock_unavailable",
        "load_failed",
        "compact_failed",
        "issue_append_failed",
        "consume_append_failed",
        "expired_append_failed",
        "denied_append_failed",
        "init_failed",
        UnknownStateUnavailable
    };
    public const int ApprovalTokenTtlSecondsDefault = 600;
    public static readonly DateTime RunIdCutoverUtc = new(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime CapabilityFingerprintCutoverUtc = new(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);
    private readonly HashSet<string> _consumedApprovalProposalIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _expiredApprovalProposalIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActionApprovalProposal> _approvalProposals = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _approvalLedgerLock = new();
    private FileStream? _approvalRuntimeOwnerLockHandle;
    private string? _approvalRuntimeOwnerLockPath;
    private bool _approvalLedgerInitialized;
    private bool _approvalLedgerHealthy = true;
    private string _approvalLedgerError = string.Empty;
    private ApprovalLedgerV2? _approvalLedger;

    public required string SessionId { get; init; }
    public required string RuntimeRoot { get; init; }
    public required string ActiveWorkspaceRoot { get; set; }
    public string? ExecutionWorkspaceRoot { get; set; }
    public string? WorktreeRoot { get; set; }
    public string ExecutionWorkspaceKind { get; set; } = "active-workspace";
    public bool ActiveWorkspaceUsed { get; set; } = true;
    public required AgentAccessMode AccessMode { get; set; }
    public required ProtectedPathPolicy ProtectedPathPolicy { get; init; }
    public Func<DateTime> UtcNowProvider { get; init; } = static () => DateTime.UtcNow;
    public string ApprovalRuntimeOwnerLockPath => _approvalRuntimeOwnerLockPath ??= ComputeApprovalRuntimeOwnerLockPath(RuntimeRoot);
    public bool IsApprovalLedgerHealthy
    {
        get
        {
            EnsureApprovalLedgerInitialized();
            return _approvalLedgerHealthy;
        }
    }
    public string ApprovalLedgerError
    {
        get
        {
            EnsureApprovalLedgerInitialized();
            return GetPublicApprovalLedgerErrorCode();
        }
    }

    public bool IsApprovalProposalConsumed(string proposalId)
    {
        EnsureApprovalLedgerInitialized();
        if (string.IsNullOrWhiteSpace(proposalId))
            return false;

        lock (_consumedApprovalProposalIds)
            return _consumedApprovalProposalIds.Contains(proposalId);
    }

    public bool IsApprovalProposalExpired(string proposalId)
    {
        EnsureApprovalLedgerInitialized();
        if (string.IsNullOrWhiteSpace(proposalId))
            return false;

        lock (_expiredApprovalProposalIds)
            return _expiredApprovalProposalIds.Contains(proposalId);
    }

    public static string CreateRunId() => Guid.NewGuid().ToString("N");

    public bool ConsumeApprovalProposal(string proposalId, string? runId = null)
    {
        EnsureApprovalLedgerInitialized();
        if (string.IsNullOrWhiteSpace(proposalId))
            return false;

        if (!_approvalLedgerHealthy || _approvalLedger is null)
            return false;

        var effectiveRunId = ResolveRunIdForProposal(proposalId, runId);
        var capabilityFingerprint = ResolveCapabilityFingerprintForProposal(proposalId);
        if (!_approvalLedger.TryAppendConsumed(SessionId, proposalId, UtcNowProvider(), effectiveRunId, capabilityFingerprint, out _))
        {
            _approvalLedgerHealthy = false;
            SetApprovalLedgerErrorCode("consume_append_failed");
            return false;
        }

        lock (_consumedApprovalProposalIds)
            _consumedApprovalProposalIds.Add(proposalId);
        lock (_expiredApprovalProposalIds)
            _expiredApprovalProposalIds.Remove(proposalId);
        return true;
    }

    public bool MarkApprovalProposalExpired(string proposalId, string reasonCode, string? runId = null)
    {
        EnsureApprovalLedgerInitialized();
        if (string.IsNullOrWhiteSpace(proposalId))
            return false;
        if (!_approvalLedgerHealthy || _approvalLedger is null)
            return false;
        if (IsApprovalProposalConsumed(proposalId))
            return true;
        if (IsApprovalProposalExpired(proposalId))
            return true;
        var effectiveRunId = ResolveRunIdForProposal(proposalId, runId);
        var capabilityFingerprint = ResolveCapabilityFingerprintForProposal(proposalId);
        if (!_approvalLedger.TryAppendExpired(SessionId, proposalId, UtcNowProvider(), reasonCode, effectiveRunId, capabilityFingerprint, out _))
        {
            _approvalLedgerHealthy = false;
            SetApprovalLedgerErrorCode("expired_append_failed");
            return false;
        }
        lock (_expiredApprovalProposalIds)
            _expiredApprovalProposalIds.Add(proposalId);
        return true;
    }

    public bool RecordApprovalDeniedEvent(string proposalId, string eventName, string reasonCode, string? runId = null)
    {
        EnsureApprovalLedgerInitialized();
        if (string.IsNullOrWhiteSpace(proposalId))
            return false;
        if (!_approvalLedgerHealthy || _approvalLedger is null)
            return false;
        var effectiveRunId = ResolveRunIdForProposal(proposalId, runId);
        if (!_approvalLedger.TryAppendDenied(SessionId, proposalId, eventName, UtcNowProvider(), reasonCode, effectiveRunId, out _))
        {
            _approvalLedgerHealthy = false;
            SetApprovalLedgerErrorCode("denied_append_failed");
            return false;
        }
        return true;
    }

    public bool RegisterApprovalProposal(ActionApprovalProposal proposal)
    {
        EnsureApprovalLedgerInitialized();
        if (proposal is null || string.IsNullOrWhiteSpace(proposal.ProposalId))
            return false;
        if (!_approvalLedgerHealthy || _approvalLedger is null)
            return false;

        if (!_approvalLedger.TryAppendIssued(proposal, out _))
        {
            _approvalLedgerHealthy = false;
            SetApprovalLedgerErrorCode("issue_append_failed");
            return false;
        }

        lock (_approvalProposals)
            _approvalProposals[proposal.ProposalId] = proposal;
        return true;
    }

    public ActionApprovalProposal? GetApprovalProposal(string proposalId)
    {
        EnsureApprovalLedgerInitialized();
        if (string.IsNullOrWhiteSpace(proposalId))
            return null;
        lock (_approvalProposals)
            return _approvalProposals.TryGetValue(proposalId, out var proposal) ? proposal : null;
    }

    private string? ResolveRunIdForProposal(string proposalId, string? runId)
    {
        var normalized = NormalizeOptionalRunId(runId);
        if (!string.IsNullOrWhiteSpace(normalized))
            return normalized;
        var proposal = GetApprovalProposal(proposalId);
        return NormalizeOptionalRunId(proposal?.RunId);
    }

    private static string? NormalizeOptionalRunId(string? runId)
    {
        var normalized = runId?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private CapabilityFingerprintV1? ResolveCapabilityFingerprintForProposal(string proposalId)
    {
        var proposal = GetApprovalProposal(proposalId);
        return proposal?.CapabilityFingerprint;
    }

    private void EnsureApprovalLedgerInitialized()
    {
        if (_approvalLedgerInitialized)
            return;

        lock (_approvalLedgerLock)
        {
            if (_approvalLedgerInitialized)
                return;
            _approvalLedgerInitialized = true;
            try
            {
                if (!TryAcquireApprovalRuntimeOwnerLock())
                {
                    _approvalLedgerHealthy = false;
                    SetApprovalLedgerErrorCode("owner_lock_unavailable");
                    return;
                }
                _approvalLedger = new ApprovalLedgerV2(RuntimeRoot);
                if (!_approvalLedger.TryCompactForStartup(UtcNowProvider(), ApprovalTokenTtlSecondsDefault, out _, out _))
                {
                    _approvalLedgerHealthy = false;
                    SetApprovalLedgerErrorCode("compact_failed");
                    return;
                }
                if (!_approvalLedger.TryLoad(out var proposals, out var consumed, out var expired, out _))
                {
                    _approvalLedgerHealthy = false;
                    SetApprovalLedgerErrorCode("load_failed");
                    return;
                }

                foreach (var pair in proposals)
                    _approvalProposals[pair.Key] = pair.Value;
                foreach (var id in consumed)
                    _consumedApprovalProposalIds.Add(id);
                foreach (var id in expired)
                    _expiredApprovalProposalIds.Add(id);
            }
            catch (Exception)
            {
                _approvalLedgerHealthy = false;
                SetApprovalLedgerErrorCode("init_failed");
            }
        }
    }

    private bool TryAcquireApprovalRuntimeOwnerLock()
    {
        if (_approvalRuntimeOwnerLockHandle is not null)
            return true;

        try
        {
            var lockPath = ApprovalRuntimeOwnerLockPath;
            var lockDir = Path.GetDirectoryName(lockPath);
            if (!string.IsNullOrWhiteSpace(lockDir))
                Directory.CreateDirectory(lockDir);
            _approvalRuntimeOwnerLockHandle = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void SetApprovalLedgerErrorCode(string? code)
    {
        _approvalLedgerError = NormalizeApprovalLedgerErrorCode(code);
    }

    private string GetPublicApprovalLedgerErrorCode() =>
        NormalizeApprovalLedgerErrorCode(_approvalLedgerError);

    private static string NormalizeApprovalLedgerErrorCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return UnknownStateUnavailable;

        var normalized = code.Trim();
        var separator = normalized.IndexOf(':');
        if (separator > 0)
            normalized = normalized[..separator].Trim();

        return KnownApprovalLedgerErrorCodes.Contains(normalized)
            ? normalized
            : UnknownStateUnavailable;
    }

    private static string ComputeApprovalRuntimeOwnerLockPath(string runtimeRoot)
    {
        var normalizedRuntimeRoot = Path.GetFullPath(runtimeRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedRuntimeRoot))).ToLowerInvariant();
        return Path.Combine(Path.GetTempPath(), "LocalCursorAgent", "approval-runtime-owner-locks", $"approval-runtime-owner-{hash}.lock");
    }

    public void Dispose()
    {
        lock (_approvalLedgerLock)
        {
            _approvalRuntimeOwnerLockHandle?.Dispose();
            _approvalRuntimeOwnerLockHandle = null;
        }
        GC.SuppressFinalize(this);
    }

    ~AgentSessionContext()
    {
        try
        {
            _approvalRuntimeOwnerLockHandle?.Dispose();
        }
        catch
        {
            // best-effort cleanup for abandoned session objects
        }
    }
}
