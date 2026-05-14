using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Tools;

namespace LocalCursorAgent.Security;

public sealed class GuardedTool : ITool
{
    private readonly ITool _inner;
    private readonly PermissionGuard _guard;
    private readonly AgentSessionContext _session;
    private readonly Func<string, ToolAction> _actionFactory;
    private readonly ExecutionTracer? _tracer;
    private readonly Dictionary<string, string> _proposalRunBindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _proposalRunBindingsLock = new();

    public GuardedTool(ITool inner, PermissionGuard guard, AgentSessionContext session, Func<string, ToolAction> actionFactory, ExecutionTracer? tracer = null)
    {
        _inner = inner;
        _guard = guard;
        _session = session;
        _actionFactory = actionFactory;
        _tracer = tracer;
    }

    public string Name => _inner.Name;
    public string Description => _inner.Description;

    public async Task<string> Execute(string input)
    {
        var action = _actionFactory(input);
        string consumedProposalId = string.Empty;
        string? consumedRunId = null;
        if (CommandRiskPolicy.TryExtractApprovalToken(input, out var token))
        {
            action = SanitizeApprovalTokenFromPaths(action);
            var runId = TryGetRunBinding(token);
            action = BuildActionWithApprovalToken(action, token, runId);
        }
        var decision = _guard.Evaluate(_session, action);
        CacheRunBinding(decision);
        if (decision.Allowed && CommandRiskPolicy.TryExtractApprovalToken(action.Payload, out var approvedToken))
        {
            consumedProposalId = approvedToken;
            consumedRunId = action.RunId;
        }
        _tracer?.LogPermissionDecision(_session, _inner.Name, action, decision);

        if (!decision.Allowed)
            return $"DENIED [{decision.ReasonCodeName}]: {decision.Message}";

        try
        {
            var result = await _inner.Execute(input);
            if (!string.IsNullOrWhiteSpace(consumedProposalId))
            {
                if (!_session.ConsumeApprovalProposal(consumedProposalId, consumedRunId))
                    return $"DENIED [{PermissionReasonCodes.ApprovalStateUnavailable}]: Approval state unavailable: {_session.ApprovalLedgerError}";
            }
            _tracer?.LogActionExecution(_inner.Name, action, decision, succeeded: true, PermissionReasonCodes.Allowed, "Action executed.");
            return result;
        }
        catch (Exception ex)
        {
            _tracer?.LogActionExecution(_inner.Name, action, decision, succeeded: false, PermissionReasonCodes.ToolDeniedByPolicy, ex.Message);
            throw;
        }
    }

    private void CacheRunBinding(PermissionDecision decision)
    {
        var proposal = decision.ApprovalProposal;
        if (proposal is null || string.IsNullOrWhiteSpace(proposal.ProposalId) || string.IsNullOrWhiteSpace(proposal.RunId))
            return;
        lock (_proposalRunBindingsLock)
            _proposalRunBindings[proposal.ProposalId] = proposal.RunId;
    }

    private string? TryGetRunBinding(string proposalId)
    {
        lock (_proposalRunBindingsLock)
            return _proposalRunBindings.TryGetValue(proposalId, out var runId) ? runId : null;
    }

    private static ToolAction BuildActionWithApprovalToken(ToolAction action, string proposalId, string? runId)
    {
        var payload = action.Payload;
        if (!CommandRiskPolicy.HasExplicitApprovalMarker(payload))
        {
            payload = string.IsNullOrWhiteSpace(payload)
                ? $"APPROVED:{proposalId}"
                : $"{payload} APPROVED:{proposalId}";
        }

        return new ToolAction
        {
            Kind = action.Kind,
            RunId = string.IsNullOrWhiteSpace(runId) ? action.RunId : runId,
            TargetPath = action.TargetPath,
            SourcePath = action.SourcePath,
            DestinationPath = action.DestinationPath,
            WorkingDirectory = action.WorkingDirectory,
            Payload = payload
        };
    }

    private static ToolAction SanitizeApprovalTokenFromPaths(ToolAction action) => new()
    {
        Kind = action.Kind,
        RunId = action.RunId,
        TargetPath = StripApprovalMarkerFromPath(action.TargetPath),
        SourcePath = StripApprovalMarkerFromPath(action.SourcePath),
        DestinationPath = StripApprovalMarkerFromPath(action.DestinationPath),
        WorkingDirectory = StripApprovalMarkerFromPath(action.WorkingDirectory),
        Payload = action.Payload
    };

    private static string? StripApprovalMarkerFromPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var marker = "APPROVED:";
        var idx = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return value;

        var tokenEnd = value.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }, idx + marker.Length);
        return tokenEnd >= 0
            ? value.Remove(idx, tokenEnd - idx).Trim()
            : value[..idx].Trim();
    }
}
