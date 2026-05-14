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
        action = BindRunIdFromApprovalTokenIfAvailable(input, action);
        string consumedProposalId = string.Empty;
        string? consumedRunId = null;
        if (CommandRiskPolicy.TryExtractApprovalToken(input, out _))
            action = SanitizeApprovalTokenFromPaths(action);
        var decision = _guard.Evaluate(_session, action);
        if (!decision.Allowed && decision.RequiresApproval && TryApplyProposalBoundApproval(input, action, decision, out var approvedAction, out var approvedDecision, out consumedProposalId, out consumedRunId))
        {
            action = approvedAction;
            decision = approvedDecision;
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

    private bool TryApplyProposalBoundApproval(string input, ToolAction action, PermissionDecision decision, out ToolAction approvedAction, out PermissionDecision approvedDecision, out string consumedProposalId, out string? consumedRunId)
    {
        approvedAction = action;
        approvedDecision = decision;
        consumedProposalId = string.Empty;
        consumedRunId = null;
        if (!CommandRiskPolicy.TryExtractApprovalToken(input, out var token))
            return false;

        var expectedToken = decision.ExpectedApprovalToken;
        if (string.IsNullOrWhiteSpace(expectedToken))
            return false;

        var expectedId = expectedToken["APPROVED:".Length..];
        if (!token.Equals(expectedId, StringComparison.OrdinalIgnoreCase))
            return false;
        var proposal = _session.GetApprovalProposal(expectedId);
        if (proposal is not null && _session.UtcNowProvider() > proposal.ExpiresAtUtc)
        {
            approvedDecision = PermissionDecision.Deny(PermissionReasonCode.ApprovalTokenExpired, "Approval token expired.");
            return true;
        }
        if (_session.IsApprovalProposalConsumed(expectedId))
            return false;

        approvedAction = CreateApprovedAction(action, expectedToken, proposal?.RunId);
        approvedDecision = _guard.Evaluate(_session, approvedAction);
        if (!approvedDecision.Allowed)
            return false;
        consumedProposalId = expectedId;
        consumedRunId = proposal?.RunId;
        return true;
    }

    private ToolAction BindRunIdFromApprovalTokenIfAvailable(string input, ToolAction action)
    {
        if (!CommandRiskPolicy.TryExtractApprovalToken(input, out var token))
            return action;
        var proposal = _session.GetApprovalProposal(token);
        var runId = proposal?.RunId;
        if (string.IsNullOrWhiteSpace(runId))
            return action;
        return new ToolAction
        {
            Kind = action.Kind,
            RunId = runId,
            TargetPath = action.TargetPath,
            SourcePath = action.SourcePath,
            DestinationPath = action.DestinationPath,
            WorkingDirectory = action.WorkingDirectory,
            Payload = action.Payload
        };
    }

    private static ToolAction CreateApprovedAction(ToolAction action, string expectedToken, string? runId) => new()
    {
        Kind = action.Kind,
        RunId = string.IsNullOrWhiteSpace(runId) ? action.RunId : runId,
        TargetPath = action.TargetPath,
        SourcePath = action.SourcePath,
        DestinationPath = action.DestinationPath,
        WorkingDirectory = action.WorkingDirectory,
        Payload = string.IsNullOrWhiteSpace(action.Payload)
            ? expectedToken
            : $"{action.Payload} {expectedToken}"
    };

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
