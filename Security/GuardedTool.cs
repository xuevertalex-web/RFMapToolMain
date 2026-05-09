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
        if (CommandRiskPolicy.TryExtractApprovalToken(input, out _))
            action = SanitizeApprovalTokenFromPaths(action);
        var decision = _guard.Evaluate(_session, action);
        if (!decision.Allowed && decision.RequiresApproval && TryApplyProposalBoundApproval(input, action, decision, out var approvedAction, out var approvedDecision))
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
            _tracer?.LogActionExecution(_inner.Name, action, decision, succeeded: true, PermissionReasonCodes.Allowed, "Action executed.");
            return result;
        }
        catch (Exception ex)
        {
            _tracer?.LogActionExecution(_inner.Name, action, decision, succeeded: false, PermissionReasonCodes.ToolDeniedByPolicy, ex.Message);
            throw;
        }
    }

    private bool TryApplyProposalBoundApproval(string input, ToolAction action, PermissionDecision decision, out ToolAction approvedAction, out PermissionDecision approvedDecision)
    {
        approvedAction = action;
        approvedDecision = decision;
        if (!CommandRiskPolicy.TryExtractApprovalToken(input, out var token))
            return false;

        var expectedToken = decision.ExpectedApprovalToken;
        if (string.IsNullOrWhiteSpace(expectedToken))
            return false;

        var expectedId = expectedToken["APPROVED:".Length..];
        if (!token.Equals(expectedId, StringComparison.OrdinalIgnoreCase))
            return false;

        approvedAction = CreateApprovedAction(action);
        approvedDecision = _guard.Evaluate(_session, approvedAction);
        return true;
    }

    private static ToolAction CreateApprovedAction(ToolAction action) => new()
    {
        Kind = action.Kind,
        TargetPath = action.TargetPath,
        SourcePath = action.SourcePath,
        DestinationPath = action.DestinationPath,
        WorkingDirectory = action.WorkingDirectory,
        Payload = string.IsNullOrWhiteSpace(action.Payload)
            ? "APPROVED:token"
            : $"{action.Payload} APPROVED:token"
    };

    private static ToolAction SanitizeApprovalTokenFromPaths(ToolAction action) => new()
    {
        Kind = action.Kind,
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
