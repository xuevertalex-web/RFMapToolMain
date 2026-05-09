using System;
using System.IO;
using System.Text.Json;

namespace LocalCursorAgent.Diagnostics;

public partial class ExecutionTracer
{
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
