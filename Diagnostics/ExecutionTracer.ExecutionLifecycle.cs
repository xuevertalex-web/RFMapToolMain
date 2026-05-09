using System;
using System.Collections.Generic;

namespace LocalCursorAgent.Diagnostics;

public partial class ExecutionTracer
{
    public void LogExecutionStart(string query, DateTime timestamp)
    {
        _executionStartTime = timestamp;

        var entry = new ExecutionLogEntry
        {
            Timestamp = timestamp,
            EventType = "ExecutionStart",
            Message = query,
            Details = new Dictionary<string, object>
            {
                { "ThreadId", System.Threading.Thread.CurrentThread.ManagedThreadId }
            }
        };

        _executionLog.Add(entry);
        AppendToTraceFile(entry);
    }

    public double GetExecutionDuration()
    {
        return (DateTime.UtcNow - _executionStartTime).TotalMilliseconds;
    }
}
