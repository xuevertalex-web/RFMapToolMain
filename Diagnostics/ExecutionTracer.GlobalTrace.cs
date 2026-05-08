using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LocalCursorAgent.Security;

namespace LocalCursorAgent.Diagnostics;

public partial class ExecutionTracer
{
    public void LogExecutionEnd(DateTime timestamp, TimeSpan duration, string outcome)
    {
        var entry = new ExecutionLogEntry
        {
            Timestamp = timestamp,
            EventType = "ExecutionEnd",
            Outcome = outcome,
            Duration = duration.TotalMilliseconds,
            Details = new Dictionary<string, object>()
        };

        _executionLog.Add(entry);
        AppendToTraceFile(entry);
    }

    public void LogEvent(string eventType, string message, Dictionary<string, object>? details = null)
    {
        var entry = new ExecutionLogEntry
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Message = message,
            Details = details ?? new Dictionary<string, object>()
        };

        _executionLog.Add(entry);
        AppendToTraceFile(entry);
        LogActionEvent(eventType, "Legacy", ActionLogLevel.Debug, entry.Outcome ?? "logged", metadata: new Dictionary<string, object?>
        {
            { "message", message },
            { "details", details ?? new Dictionary<string, object>() }
        });
    }

    public void LogTargetResolution(string query, string targetToken, IReadOnlyList<string> symbolCandidates, IReadOnlyList<string> filenameCandidates, IReadOnlyList<string> selectedFiles, bool safeFailure, string? failureMessage)
    {
        LogEvent("TargetResolution", "Resolved task target", new Dictionary<string, object>
        {
            { "Query", query },
            { "TargetToken", targetToken },
            { "SymbolCandidates", symbolCandidates.ToArray() },
            { "FilenameCandidates", filenameCandidates.ToArray() },
            { "SelectedFiles", selectedFiles.ToArray() },
            { "SafeFailure", safeFailure },
            { "FailureMessage", failureMessage ?? string.Empty }
        });
    }

    public void LogTargetResolutionGate(string query, string rawTargetToken, string classification, IReadOnlyList<string> exactSymbolCandidates, IReadOnlyList<string> exactFilenameCandidates, IReadOnlyList<string> partialCandidates, IReadOnlyList<string> semanticCandidates, IReadOnlyList<string> selectedFiles, string outcome, string reasonCode, string reason, double confidence)
    {
        LogEvent("TargetResolutionGate", "Evaluated exact target gate", new Dictionary<string, object>
        {
            { "Query", query },
            { "RawTargetToken", rawTargetToken },
            { "Classification", classification },
            { "ExactSymbolCandidates", exactSymbolCandidates.ToArray() },
            { "ExactFilenameCandidates", exactFilenameCandidates.ToArray() },
            { "PartialCandidates", partialCandidates.ToArray() },
            { "SemanticCandidates", semanticCandidates.ToArray() },
            { "SelectedFiles", selectedFiles.ToArray() },
            { "Outcome", outcome },
            { "ReasonCode", reasonCode },
            { "Reason", reason },
            { "Confidence", confidence }
        });
    }

    public void LogIntentConfirmationGate(string rawIntent, string classifiedKind, bool mutationLike, bool targetConfirmed, string outcome, string reasonCode, string reason, string resolvedTarget, IReadOnlyList<string> evidence)
    {
        LogEvent("IntentConfirmationGate", "Evaluated first actionable intent", new Dictionary<string, object>
        {
            { "RawIntent", rawIntent },
            { "ClassifiedKind", classifiedKind },
            { "MutationLike", mutationLike },
            { "TargetConfirmed", targetConfirmed },
            { "Outcome", outcome },
            { "ReasonCode", reasonCode },
            { "Reason", reason },
            { "ResolvedTarget", resolvedTarget },
            { "Evidence", evidence.ToArray() }
        });
    }

    public void LogMultiFileEditGate(string rawIntent, string classifiedKind, bool explicitMultiFile, bool intentConfirmed, IReadOnlyList<string> plannedMutationFiles, IReadOnlyList<string> confirmedTargetFiles, string outcome, string reasonCode, string reason)
    {
        LogEvent("MultiFileEditGate", "Evaluated multi-file mutation intent", new Dictionary<string, object>
        {
            { "RawIntent", rawIntent },
            { "ClassifiedKind", classifiedKind },
            { "ExplicitMultiFile", explicitMultiFile },
            { "IntentConfirmed", intentConfirmed },
            { "PlannedMutationFiles", plannedMutationFiles.ToArray() },
            { "ConfirmedTargetFiles", confirmedTargetFiles.ToArray() },
            { "Outcome", outcome },
            { "ReasonCode", reasonCode },
            { "Reason", reason }
        });
    }

    public void LogWorkspaceResolution(string? seedPath, WorkspaceResolutionResult resolution, string runtimeRoot)
    {
        _lastWorkspaceResolution = new WorkspaceResolutionSnapshot
        {
            SeedPath = seedPath ?? string.Empty,
            RuntimeRoot = runtimeRoot,
            Success = resolution.Success,
            Reason = resolution.Reason.ToString(),
            ReasonCode = resolution.ReasonCode,
            ReasonCodeName = resolution.ReasonCodeName,
            Message = resolution.Message,
            WorkspaceRoot = resolution.WorkspaceRoot ?? string.Empty,
            Source = resolution.Source ?? string.Empty
        };

        LogEvent("WorkspaceResolution", "Workspace resolved", new Dictionary<string, object>
        {
            { "SeedPath", seedPath ?? string.Empty },
            { "RuntimeRoot", runtimeRoot },
            { "Success", resolution.Success },
            { "ReasonCode", resolution.ReasonCode },
            { "ReasonCodeName", resolution.ReasonCodeName },
            { "Reason", resolution.Reason.ToString() },
            { "Message", resolution.Message },
            { "WorkspaceRoot", resolution.WorkspaceRoot ?? string.Empty },
            { "Source", resolution.Source ?? string.Empty }
        });
    }

    public void LogSessionHeader(AgentSessionContext session, IReadOnlyList<string> protectedRoots)
    {
        _lastSessionHeader = new SessionHeader
        {
            SessionId = session.SessionId,
            RuntimeRoot = session.RuntimeRoot,
            WorkspaceRoot = session.ActiveWorkspaceRoot,
            AccessMode = session.AccessMode.ToString(),
            ProtectedRoots = protectedRoots.ToArray()
        };

        LogEvent("SessionHeader", "Agent session initialized", new Dictionary<string, object>
        {
            { "SessionId", session.SessionId },
            { "RuntimeRoot", session.RuntimeRoot },
            { "WorkspaceRoot", session.ActiveWorkspaceRoot },
            { "AccessMode", session.AccessMode.ToString() },
            { "ProtectedRoots", protectedRoots.ToArray() }
        });
    }

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

    public IReadOnlyList<ActionApprovalProposal> GetApprovalRequiredActions() => _approvalRequiredActions.ToArray();
    public int GetDeniedPermissionDecisionCount() => _deniedPermissionDecisions;
    public IReadOnlyList<ActionLifecycleEntry> GetActionLedger() => _actionLedger.ToArray();

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
            {
                _pendingActionCorrelations.Remove(actionSignature);
            }

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

    public void LogDestructiveOperation(DestructiveTraceRecord record)
    {
        LogEvent("DestructiveOperation", "Destructive lifecycle step", new Dictionary<string, object>
        {
            { "OperationKind", record.OperationKind },
            { "Step", record.Step },
            { "OriginalPath", record.OriginalPath },
            { "TargetPath", record.TargetPath ?? string.Empty },
            { "SnapshotPath", record.SnapshotPath ?? string.Empty },
            { "PreviewAccepted", record.PreviewAccepted },
            { "ApplySucceeded", record.ApplySucceeded },
            { "ApplyFailed", record.ApplyFailed },
            { "RollbackSucceeded", record.RollbackSucceeded },
            { "RollbackFailed", record.RollbackFailed },
            { "CommitSucceeded", record.CommitSucceeded },
            { "CommitFailed", record.CommitFailed },
            { "ReasonCode", record.ReasonCode ?? string.Empty },
            { "TimestampUtc", record.TimestampUtc.ToString("O") },
            { "StepOrder", record.StepOrder }
        });
    }

    public void LogPatchLifecycle(PatchTraceRecord record)
    {
        LogEvent("PatchLifecycle", "Patch lifecycle step", new Dictionary<string, object>
        {
            { "OperationKind", record.OperationKind },
            { "Step", record.Step },
            { "TargetPath", record.TargetPath },
            { "SnapshotHashBeforeApply", record.SnapshotHashBeforeApply ?? string.Empty },
            { "PreviewAccepted", record.PreviewAccepted },
            { "ApplySucceeded", record.ApplySucceeded },
            { "ApplyFailed", record.ApplyFailed },
            { "RollbackSucceeded", record.RollbackSucceeded },
            { "RollbackFailed", record.RollbackFailed },
            { "ReasonCode", record.ReasonCode ?? string.Empty },
            { "TimestampUtc", record.TimestampUtc.ToString("O") },
            { "StepOrder", record.StepOrder }
        });
    }

    private void AppendToTraceFile(ExecutionLogEntry entry)
    {
        try
        {
            File.AppendAllText(_traceFile, $"{FormatExecutionLogEntry(entry)}{Environment.NewLine}");
        }
        catch
        {
            // Silent fail - logging should never crash the system
        }
    }

    private void AppendTimelineLine(ActionEvent entry)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_currentRunId))
                return;

            var line = FormatActionEvent(entry);
            File.AppendAllText(_timelineFile, $"{line}{Environment.NewLine}");
            File.AppendAllText(Path.Combine(_humanRootDirectory, $"{_currentRunId}.timeline.log"), $"{line}{Environment.NewLine}");
        }
        catch
        {
            // Silent fail
        }
    }

    private void AppendJsonLine<T>(string path, T payload)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
            File.AppendAllText(path, $"{json}\n");
        }
        catch
        {
            // Silent fail - logging should never crash the system
        }
    }
}