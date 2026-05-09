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
        if (CommandRiskPolicy.HasExplicitApprovalMarker(input))
            action = CreateApprovedAction(action);

        var decision = _guard.Evaluate(_session, action);
        if (!decision.Allowed && decision.RequiresApproval && CommandRiskPolicy.HasExplicitApprovalMarker(input))
        {
            var approvedAction = CreateApprovedAction(action);
            decision = _guard.Evaluate(_session, approvedAction);
            action = approvedAction;
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

    private static string? StripApprovalMarker(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var marker = "APPROVED:true";
        var idx = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return value;

        return value.Remove(idx, marker.Length).Trim();
    }

    private static ToolAction CreateApprovedAction(ToolAction action) => new()
    {
        Kind = action.Kind,
        TargetPath = StripApprovalMarker(action.TargetPath),
        SourcePath = StripApprovalMarker(action.SourcePath),
        DestinationPath = StripApprovalMarker(action.DestinationPath),
        WorkingDirectory = StripApprovalMarker(action.WorkingDirectory),
        Payload = string.IsNullOrWhiteSpace(action.Payload)
            ? "APPROVED:true"
            : $"{action.Payload} APPROVED:true"
    };

}
