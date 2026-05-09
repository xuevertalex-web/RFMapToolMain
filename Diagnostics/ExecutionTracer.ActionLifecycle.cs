using System;
using System.Collections.Generic;
using System.Text.Json;
using LocalCursorAgent.Security;

namespace LocalCursorAgent.Diagnostics;

public partial class ExecutionTracer
{
    public void LogPermissionDecision(AgentSessionContext session, string toolName, ToolAction action, PermissionDecision decision)
    {
        AppendActionLifecycle(toolName, action, decision, ActionLifecycleState.Requested, decision.ReasonCodeString, decision.Message);
        if (!decision.Allowed && !decision.RequiresApproval)
            _deniedPermissionDecisions++;

        if (decision.RequiresApproval && decision.ApprovalProposal is not null)
        {
            _approvalRequiredActions.Add(decision.ApprovalProposal);
            AppendActionLifecycle(toolName, action, decision, ActionLifecycleState.ApprovalRequired, decision.ReasonCodeString, decision.Message);
        }
        else if (!decision.Allowed)
        {
            AppendActionLifecycle(toolName, action, decision, ActionLifecycleState.Blocked, decision.ReasonCodeString, decision.Message);
        }

        LogEvent("PermissionDecision", "Tool permission evaluated", new Dictionary<string, object>
        {
            { "SessionId", session.SessionId },
            { "ToolName", toolName },
            { "ActionKind", action.Kind.ToString() },
            { "TargetPath", action.TargetPath ?? string.Empty },
            { "SourcePath", action.SourcePath ?? string.Empty },
            { "DestinationPath", action.DestinationPath ?? string.Empty },
            { "WorkingDirectory", action.WorkingDirectory ?? string.Empty },
            { "Allowed", decision.Allowed },
            { "ReasonCode", decision.ReasonCodeString },
            { "ReasonCodeName", decision.ReasonCodeName },
            { "Message", decision.Message },
            { "NormalizedTargetPath", decision.NormalizedTargetPath ?? string.Empty },
            { "NormalizedWorkspaceRoot", decision.NormalizedWorkspaceRoot ?? string.Empty },
            { "RequiresApproval", decision.RequiresApproval },
            { "ApprovalStatus", decision.ApprovalStatus.ToString() },
            { "ApprovalProposal", decision.ApprovalProposal is null ? string.Empty : JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    { "actionType", decision.ApprovalProposal.ActionType },
                    { "command", decision.ApprovalProposal.Command ?? string.Empty },
                    { "path", decision.ApprovalProposal.Path ?? string.Empty },
                    { "normalizedTarget", decision.ApprovalProposal.NormalizedTarget ?? string.Empty },
                    { "sandboxRoot", decision.ApprovalProposal.SandboxRoot },
                    { "projectRoot", decision.ApprovalProposal.ProjectRoot },
                    { "worktreeRoot", decision.ApprovalProposal.WorktreeRoot },
                    { "riskLevel", decision.ApprovalProposal.RiskLevel },
                    { "reason", decision.ApprovalProposal.Reason },
                    { "approvalStatus", decision.ApprovalProposal.ApprovalStatus.ToString() }
                })
            },
            { "AccessMode", session.AccessMode.ToString() }
        });
    }

    public void LogActionExecution(string toolName, ToolAction action, PermissionDecision? decision, bool succeeded, string reasonCode, string reason)
    {
        AppendActionLifecycle(toolName, action, decision, succeeded ? ActionLifecycleState.Executed : ActionLifecycleState.Failed, reasonCode, reason);
    }

    private void AppendActionLifecycle(string toolName, ToolAction action, PermissionDecision? decision, ActionLifecycleState state, string reasonCode, string reason)
    {
        var normalizedTarget = decision?.NormalizedTargetPath ?? action.TargetPath ?? action.SourcePath ?? action.DestinationPath ?? action.WorkingDirectory ?? string.Empty;
        var actionSignature = BuildActionSignature(toolName, action, normalizedTarget);
        var correlationId = ResolveActionCorrelationId(actionSignature, state);
        _actionLedger.Add(new ActionLifecycleEntry
        {
            Sequence = _actionLedger.Count + 1,
            ActionCorrelationId = correlationId,
            ToolName = toolName ?? string.Empty,
            ActionType = action.Kind.ToString(),
            Target = action.TargetPath ?? action.SourcePath ?? action.DestinationPath ?? action.WorkingDirectory ?? string.Empty,
            Command = action.Kind == ToolActionKind.RunCommand ? action.Payload ?? string.Empty : string.Empty,
            NormalizedTarget = normalizedTarget,
            LifecycleState = state,
            ReasonCode = reasonCode ?? string.Empty,
            Reason = reason ?? string.Empty,
            ApprovalStatus = decision?.ApprovalStatus.ToString() ?? ApprovalStatus.NotApplicable.ToString(),
            IsInsideSandbox = decision?.Allowed == true,
            TimestampUtc = DateTime.UtcNow
        });
    }

    private string ResolveActionCorrelationId(string actionSignature, ActionLifecycleState state)
    {
        if (state == ActionLifecycleState.Requested)
        {
            var created = $"act-{Guid.NewGuid():N}";
            _pendingActionCorrelations[actionSignature] = created;
            return created;
        }

        if (_pendingActionCorrelations.TryGetValue(actionSignature, out var existing))
        {
            if (state is ActionLifecycleState.Executed or ActionLifecycleState.Failed or ActionLifecycleState.Blocked or ActionLifecycleState.ApprovalRequired)
                _pendingActionCorrelations.Remove(actionSignature);

            return existing;
        }

        return $"act-{Guid.NewGuid():N}";
    }

    private static string BuildActionSignature(string toolName, ToolAction action, string normalizedTarget)
    {
        return string.Concat(
            toolName ?? string.Empty, "|",
            action.Kind.ToString(), "|",
            normalizedTarget ?? string.Empty, "|",
            action.Payload ?? string.Empty);
    }
}
