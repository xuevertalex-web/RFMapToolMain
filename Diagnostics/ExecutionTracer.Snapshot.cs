using System;
using System.IO;
using System.Text.Json;

namespace LocalCursorAgent.Diagnostics;

public partial class ExecutionTracer
{
    public void GenerateExecutionSnapshot(string query, TimeSpan duration, string outcome)
    {
        var snapshotFileName = $"run_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        var snapshot = new ExecutionSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Query = query,
            Duration = duration,
            Outcome = outcome,
            ExecutionLog = _executionLog,
            FileTraces = _fileTraces,
            ScoringBreakdowns = _scoringBreakdowns,
            MemoryInfluences = _memoryInfluences,
            PatchDecisions = _patchDecisions,
            BuildResults = _buildResults,
            MemoryUpdates = _memoryUpdates,
            SessionHeader = _lastSessionHeader,
            WorkspaceResolution = _lastWorkspaceResolution,
            DiagnosticSummary = GenerateDiagnosticSummary(),
            Recommendations = GenerateRecommendations()
        };

        try
        {
            var filePath = Path.Combine(_runsDirectory, snapshotFileName);
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            WriteSessionManifest(snapshotFileName, snapshot);
        }
        catch
        {
            // Silent fail
        }
    }
}
