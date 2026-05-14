namespace LocalCursorAgent.Security;

public sealed class AgentSessionContext
{
    public const int ApprovalTokenTtlSecondsDefault = 600;
    private readonly HashSet<string> _consumedApprovalProposalIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _expiredApprovalProposalIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActionApprovalProposal> _approvalProposals = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _approvalLedgerLock = new();
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
            return _approvalLedgerError;
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

    public bool ConsumeApprovalProposal(string proposalId)
    {
        EnsureApprovalLedgerInitialized();
        if (string.IsNullOrWhiteSpace(proposalId))
            return false;

        if (!_approvalLedgerHealthy || _approvalLedger is null)
            return false;

        if (!_approvalLedger.TryAppendConsumed(SessionId, proposalId, UtcNowProvider(), out var appendError))
        {
            _approvalLedgerHealthy = false;
            _approvalLedgerError = $"consume_append_failed: {appendError}";
            return false;
        }

        lock (_consumedApprovalProposalIds)
            _consumedApprovalProposalIds.Add(proposalId);
        lock (_expiredApprovalProposalIds)
            _expiredApprovalProposalIds.Remove(proposalId);
        return true;
    }

    public bool MarkApprovalProposalExpired(string proposalId, string reasonCode)
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
        if (!_approvalLedger.TryAppendExpired(SessionId, proposalId, UtcNowProvider(), reasonCode, out var appendError))
        {
            _approvalLedgerHealthy = false;
            _approvalLedgerError = $"expired_append_failed: {appendError}";
            return false;
        }
        lock (_expiredApprovalProposalIds)
            _expiredApprovalProposalIds.Add(proposalId);
        return true;
    }

    public bool RecordApprovalDeniedEvent(string proposalId, string eventName, string reasonCode)
    {
        EnsureApprovalLedgerInitialized();
        if (string.IsNullOrWhiteSpace(proposalId))
            return false;
        if (!_approvalLedgerHealthy || _approvalLedger is null)
            return false;
        if (!_approvalLedger.TryAppendDenied(SessionId, proposalId, eventName, UtcNowProvider(), reasonCode, out var appendError))
        {
            _approvalLedgerHealthy = false;
            _approvalLedgerError = $"denied_append_failed: {appendError}";
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

        if (!_approvalLedger.TryAppendIssued(proposal, out var appendError))
        {
            _approvalLedgerHealthy = false;
            _approvalLedgerError = $"issue_append_failed: {appendError}";
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
                _approvalLedger = new ApprovalLedgerV2(RuntimeRoot);
                if (!_approvalLedger.TryLoad(out var proposals, out var consumed, out var expired, out var loadError))
                {
                    _approvalLedgerHealthy = false;
                    _approvalLedgerError = $"load_failed: {loadError}";
                    return;
                }

                foreach (var pair in proposals)
                    _approvalProposals[pair.Key] = pair.Value;
                foreach (var id in consumed)
                    _consumedApprovalProposalIds.Add(id);
                foreach (var id in expired)
                    _expiredApprovalProposalIds.Add(id);
            }
            catch (Exception ex)
            {
                _approvalLedgerHealthy = false;
                _approvalLedgerError = $"init_failed: {ex.Message}";
            }
        }
    }
}
