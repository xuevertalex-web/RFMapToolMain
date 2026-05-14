namespace LocalCursorAgent.Security;

public sealed class AgentSessionContext
{
    public const int ApprovalTokenTtlSecondsDefault = 600;
    private readonly HashSet<string> _consumedApprovalProposalIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActionApprovalProposal> _approvalProposals = new(StringComparer.OrdinalIgnoreCase);

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

    public bool IsApprovalProposalConsumed(string proposalId)
    {
        if (string.IsNullOrWhiteSpace(proposalId))
            return false;

        lock (_consumedApprovalProposalIds)
            return _consumedApprovalProposalIds.Contains(proposalId);
    }

    public void ConsumeApprovalProposal(string proposalId)
    {
        if (string.IsNullOrWhiteSpace(proposalId))
            return;

        lock (_consumedApprovalProposalIds)
            _consumedApprovalProposalIds.Add(proposalId);
    }

    public void RegisterApprovalProposal(ActionApprovalProposal proposal)
    {
        if (proposal is null || string.IsNullOrWhiteSpace(proposal.ProposalId))
            return;
        lock (_approvalProposals)
            _approvalProposals[proposal.ProposalId] = proposal;
    }

    public ActionApprovalProposal? GetApprovalProposal(string proposalId)
    {
        if (string.IsNullOrWhiteSpace(proposalId))
            return null;
        lock (_approvalProposals)
            return _approvalProposals.TryGetValue(proposalId, out var proposal) ? proposal : null;
    }
}
