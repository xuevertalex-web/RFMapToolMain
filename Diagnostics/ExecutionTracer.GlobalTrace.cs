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

    public IReadOnlyList<ActionApprovalProposal> GetApprovalRequiredActions() => _approvalRequiredActions.ToArray();
    public int GetDeniedPermissionDecisionCount() => _deniedPermissionDecisions;
    public IReadOnlyList<ActionLifecycleEntry> GetActionLedger() => _actionLedger.ToArray();
    public IReadOnlyList<ModelRetryAttemptDiagnostics> GetModelRetryAttemptDiagnostics()
    {
        return _actionEvents
            .Where(x => string.Equals(x.EventType, "ModelRetryAttempt", StringComparison.Ordinal))
            .OrderBy(x => x.Sequence)
            .Select(x => new ModelRetryAttemptDiagnostics
            {
                Attempt = ReadIntMetadata(x.Metadata, "attempt"),
                Reason = ReadStringMetadata(x.Metadata, "reason"),
                DelayMs = ReadIntMetadata(x.Metadata, "delay_ms"),
                WillRetry = ReadBoolMetadata(x.Metadata, "will_retry"),
                FinalAttempt = ReadBoolMetadata(x.Metadata, "final_attempt")
            })
            .ToArray();
    }

    private static int ReadIntMetadata(Dictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
            return 0;
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (int.TryParse(value.ToString(), out var parsed)) return parsed;
        return 0;
    }

    private static bool ReadBoolMetadata(Dictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
            return false;
        if (value is bool b) return b;
        if (bool.TryParse(value.ToString(), out var parsed)) return parsed;
        return false;
    }

    private static string ReadStringMetadata(Dictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
            return string.Empty;
        return value.ToString() ?? string.Empty;
    }

}
