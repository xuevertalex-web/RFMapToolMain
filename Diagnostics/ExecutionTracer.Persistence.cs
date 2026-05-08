using System.Text.Json;
using LocalCursorAgent.Security;

namespace LocalCursorAgent.Diagnostics
{
    public partial class ExecutionTracer
    {
        private void PersistRunArtifacts()
        {
            try
            {
                var manifest = new RunManifest
                {
                    RunId = _runState.RunId,
                    SessionId = _runState.SessionId,
                    StartedAtUtc = _runState.StartedAtUtc,
                    CompletedAtUtc = _runState.CompletedAtUtc,
                    DurationMs = _runState.DurationMs,
                    WorkspaceRoot = _runState.WorkspaceRoot,
                    RuntimeRoot = _runState.RuntimeRoot,
                    AccessMode = _runState.AccessMode,
                    TaskRaw = _runState.TaskRaw,
                    TaskNormalized = _runState.TaskNormalized,
                    Provider = _runState.Provider,
                    Model = _runState.Model,
                    EmbeddingsStatus = _runState.EmbeddingsStatus,
                    IndexingStatus = _runState.IndexingStatus,
                    FinalStatus = _runState.FinalStatus,
                    Summary = _runState.Summary,
                    ReasonCode = _runState.ReasonCode,
                    ChangedFiles = _runState.ChangedFiles.ToArray(),
                    BuildSucceeded = _runState.BuildSucceeded,
                    CancelSource = _runState.CancelSource,
                    DegradedFlags = new Dictionary<string, bool>(_runState.DegradedFlags),
                    StopPoint = _runState.StopPoint ?? string.Empty,
                    EventCount = _actionEvents.Count,
                    LastEventSequence = _actionEvents.LastOrDefault()?.Sequence ?? 0,
                    EventStreamFile = Path.GetFileName(_currentEventsFile),
                    SummaryFile = Path.GetFileName(_currentSummaryFile)
                };

                var latestManifestPath = GetLatestManifestPath();
                File.WriteAllText(latestManifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(Path.Combine(_runsDirectory, $"{_currentRunId}.manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

                var summary = new RunSummaryArtifact
                {
                    RunId = manifest.RunId,
                    FinalStatus = manifest.FinalStatus,
                    Summary = manifest.Summary,
                    ReasonCode = manifest.ReasonCode,
                    StartedAtUtc = manifest.StartedAtUtc,
                    CompletedAtUtc = manifest.CompletedAtUtc,
                    DurationMs = manifest.DurationMs,
                    ChangedFiles = manifest.ChangedFiles
                };

                File.WriteAllText(_currentSummaryFile, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
                var summaryText = BuildReadableRunSummary(manifest);
                File.WriteAllText(Path.Combine(_humanRootDirectory, $"{_currentRunId}.summary.txt"), summaryText);
                File.WriteAllText(GetLatestSummaryTextPath(), summaryText);
                var currentTimelinePath = Path.Combine(_humanRootDirectory, $"{_currentRunId}.timeline.log");
                if (File.Exists(currentTimelinePath))
                    File.Copy(currentTimelinePath, GetLatestTimelineTextPath(), true);
                AppendJsonLine(_catalogFile, manifest);

                if (string.Equals(manifest.FinalStatus, "succeeded", StringComparison.OrdinalIgnoreCase))
                    File.WriteAllText(GetLatestSuccessPath(), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
                else if (string.Equals(manifest.FinalStatus, "cancelled", StringComparison.OrdinalIgnoreCase))
                    File.WriteAllText(GetLatestCancelledPath(), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
                else
                    File.WriteAllText(GetLatestFailurePath(), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

                LogActionEvent("ManifestWritten", "ExecutionTracer", ActionLogLevel.Info, "persisted", metadata: new Dictionary<string, object?>
                {
                    { "latest_manifest", latestManifestPath },
                    { "events_file", _currentEventsFile },
                    { "summary_file", _currentSummaryFile },
                    { "summary_text_file", Path.Combine("human", $"{_currentRunId}.summary.txt") },
                    { "timeline_file", Path.Combine("human", $"{_currentRunId}.timeline.log") }
                });
            }
            catch
            {
                // Silent fail
            }
        }

        private void WriteSessionManifest(string snapshotFileName, ExecutionSnapshot snapshot)
        {
            try
            {
                var manifest = new SessionManifest
                {
                    GeneratedAtUtc = DateTime.UtcNow,
                    SnapshotFile = snapshotFileName,
                    SessionHeader = snapshot.SessionHeader,
                    WorkspaceResolution = snapshot.WorkspaceResolution,
                    WorkspaceResolutionReason = snapshot.WorkspaceResolution?.Reason ?? string.Empty,
                    WorkspaceResolutionReasonCode = snapshot.WorkspaceResolution?.ReasonCode ?? string.Empty,
                    WorkspaceResolutionReasonCodeName = snapshot.WorkspaceResolution?.ReasonCodeName ?? string.Empty,
                    WorkspaceResolutionSuccess = snapshot.WorkspaceResolution?.Success ?? false,
                    SessionId = snapshot.SessionHeader?.SessionId ?? string.Empty,
                    RuntimeRoot = snapshot.SessionHeader?.RuntimeRoot ?? string.Empty,
                    WorkspaceRoot = snapshot.SessionHeader?.WorkspaceRoot ?? string.Empty,
                    AccessMode = snapshot.SessionHeader?.AccessMode ?? string.Empty,
                    AccessModeDescription = AccessModeDescriptionResolver.Describe(snapshot.SessionHeader?.AccessMode),
                    ProtectedRootsCount = snapshot.SessionHeader?.ProtectedRoots?.Length ?? 0,
                    Query = snapshot.Query,
                    Outcome = snapshot.Outcome,
                    DurationMs = snapshot.Duration.TotalMilliseconds
                };

                var manifestPath = Path.Combine(_runsDirectory, "latest_manifest.json");
                var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(manifestPath, json);
            }
            catch
            {
                // Silent fail
            }
        }
    }
}
