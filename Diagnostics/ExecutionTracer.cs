using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LocalCursorAgent.Memory;
using LocalCursorAgent.Security;

namespace LocalCursorAgent.Diagnostics
{
    /// <summary>
    /// Полная система наблюдаемости и диагностики для агента.
    /// Пассивный логгер, не влияющий на принятие решений.
    /// </summary>
public class ExecutionTracer
{
    private static readonly string AppRootDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    private readonly string _logRootDirectory;
    private readonly string _machineRootDirectory;
    private readonly string _humanRootDirectory;
    private readonly string _traceFile;
    private readonly string _runsDirectory;
    private readonly string _memoryDirectory;
    private readonly string _catalogFile;
    private readonly string _timelineFile;

    private readonly List<ExecutionLogEntry> _executionLog = new();
    private readonly List<ActionEvent> _actionEvents = new();
    private readonly List<FileTraceEntry> _fileTraces = new();
    private readonly List<ScoringBreakdown> _scoringBreakdowns = new();
    private readonly List<MemoryInfluence> _memoryInfluences = new();
    private readonly List<PatchDecision> _patchDecisions = new();
    private readonly List<ActionApprovalProposal> _approvalRequiredActions = new();
    private readonly List<BuildResult> _buildResults = new();
    private readonly List<MemoryUpdate> _memoryUpdates = new();
    private SessionHeader? _lastSessionHeader;
    private WorkspaceResolutionSnapshot? _lastWorkspaceResolution;
    private DateTime _executionStartTime;
    private long _sequenceNumber;
    private string _currentRunId = string.Empty;
    private DateTime _runStartedUtc;
    private string _currentEventsFile = string.Empty;
    private string _currentSummaryFile = string.Empty;
    private RunState _runState = new();

    public ExecutionTracer(string? runtimeRoot = null)
    {
        _logRootDirectory = string.IsNullOrWhiteSpace(runtimeRoot)
            ? Path.Combine(AppRootDirectory, "logs")
            : Path.Combine(Path.GetFullPath(runtimeRoot), "logs");
        _machineRootDirectory = Path.Combine(_logRootDirectory, "machine");
        _humanRootDirectory = Path.Combine(_logRootDirectory, "human");
        _traceFile = Path.Combine(_humanRootDirectory, "execution_trace.log");
        _runsDirectory = Path.Combine(_machineRootDirectory, "runs");
        _memoryDirectory = Path.Combine(_machineRootDirectory, "memory");
        _catalogFile = Path.Combine(_runsDirectory, "runs_catalog.jsonl");
        _timelineFile = Path.Combine(_humanRootDirectory, "execution_timeline.log");

        Directory.CreateDirectory(_logRootDirectory);
        Directory.CreateDirectory(_machineRootDirectory);
        Directory.CreateDirectory(_humanRootDirectory);
        Directory.CreateDirectory(_runsDirectory);
        Directory.CreateDirectory(_memoryDirectory);
        PruneRuntimeArtifacts();
    }

    public static string GetLogDirectory() => Path.Combine(AgentRuntimePaths.ResolveRuntimeRoot(AppContext.BaseDirectory), "logs");

    public string GetLatestManifestPath() => Path.Combine(_runsDirectory, "latest_manifest.json");
    public string GetLatestSuccessPath() => Path.Combine(_runsDirectory, "latest_success.json");
    public string GetLatestFailurePath() => Path.Combine(_runsDirectory, "latest_failure.json");
    public string GetLatestCancelledPath() => Path.Combine(_runsDirectory, "latest_cancelled.json");
    public string GetLatestSummaryTextPath() => Path.Combine(_humanRootDirectory, "latest_summary.txt");
    public string GetLatestTimelineTextPath() => Path.Combine(_humanRootDirectory, "latest_timeline.log");

    public void StartRun(string taskRaw, string taskNormalized, string workspaceRoot, string runtimeRoot, string accessMode, string? provider, string? model)
    {
        _sequenceNumber = 0;
        _currentRunId = Guid.NewGuid().ToString("N");
        _runStartedUtc = DateTime.UtcNow;
        _currentEventsFile = Path.Combine(_runsDirectory, $"{_currentRunId}.events.jsonl");
        _currentSummaryFile = Path.Combine(_runsDirectory, $"{_currentRunId}.summary.json");
        _runState = new RunState
        {
            RunId = _currentRunId,
            TaskRaw = taskRaw,
            TaskNormalized = taskNormalized,
            WorkspaceRoot = workspaceRoot,
            RuntimeRoot = runtimeRoot,
            AccessMode = accessMode,
            Provider = provider ?? string.Empty,
            Model = model ?? string.Empty,
            StartedAtUtc = _runStartedUtc,
            SessionId = _lastSessionHeader?.SessionId ?? string.Empty,
            FinalStatus = "running"
        };

        LogActionEvent("RunRequested", "Program", ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
        {
            { "task_raw", taskRaw },
            { "task_normalized", taskNormalized },
            { "workspace_root", workspaceRoot },
            { "runtime_root", runtimeRoot },
            { "access_mode", accessMode },
            { "provider", provider ?? string.Empty },
            { "model", model ?? string.Empty }
        });
    }

    public void UpdateRunSession(string sessionId)
    {
        _runState.SessionId = sessionId ?? string.Empty;
    }

    public void UpdateRunEmbeddingStatus(string status, bool degraded)
    {
        _runState.EmbeddingsStatus = status ?? string.Empty;
        _runState.DegradedFlags["embeddings"] = degraded;
    }

    public void UpdateRunIndexingStatus(string status)
    {
        _runState.IndexingStatus = status ?? string.Empty;
    }

    public void MarkChangedFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (!_runState.ChangedFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
            _runState.ChangedFiles.Add(path);
    }

    public void MarkStopPoint(string component, string reasonCode, string reason, IEnumerable<string>? downstreamSuppressed = null)
    {
        _runState.StopPoint = component;
        LogActionEvent("StopPoint", component, ActionLogLevel.Warning, "stopped", reasonCode, metadata: new Dictionary<string, object?>
        {
            { "reason", reason },
            { "downstream_absence", downstreamSuppressed?.ToArray() ?? Array.Empty<string>() }
        });
    }

    public void LogActionEvent(
        string eventType,
        string component,
        ActionLogLevel level,
        string outcome,
        string? reasonCode = null,
        Dictionary<string, object?>? metadata = null,
        long? durationMs = null,
        string? correlationId = null)
    {
        var entry = new ActionEvent
        {
            RunId = _currentRunId,
            SessionId = _runState.SessionId,
            Sequence = System.Threading.Interlocked.Increment(ref _sequenceNumber),
            TimestampUtc = DateTime.UtcNow,
            EventType = eventType,
            Component = component,
            Level = level.ToString(),
            Outcome = outcome,
            ReasonCode = reasonCode ?? string.Empty,
            DurationMs = durationMs,
            CorrelationId = correlationId ?? string.Empty,
            Metadata = metadata ?? new Dictionary<string, object?>()
        };

        _actionEvents.Add(entry);
        AppendJsonLine(_currentEventsFile, entry);
        AppendTimelineLine(entry);
    }

    public IDisposable TrackDuration(string eventType, string component, ActionLogLevel level, Dictionary<string, object?>? metadata = null, string? correlationId = null)
        => new TimedActionScope(this, eventType, component, level, metadata, correlationId);

    public void CompleteRun(
        string finalStatus,
        string summary,
        string reasonCode,
        bool buildSucceeded,
        string? cancelSource = null)
    {
        _runState.FinalStatus = finalStatus;
        _runState.Summary = summary;
        _runState.ReasonCode = reasonCode;
        _runState.BuildSucceeded = buildSucceeded;
        _runState.CancelSource = cancelSource ?? string.Empty;
        _runState.CompletedAtUtc = DateTime.UtcNow;
        _runState.DurationMs = (_runState.CompletedAtUtc - _runState.StartedAtUtc).TotalMilliseconds;

        LogActionEvent("RunCompleted", "Agent", finalStatus is "failed" or "cancelled" ? ActionLogLevel.Warning : ActionLogLevel.Info, finalStatus, reasonCode, new Dictionary<string, object?>
        {
            { "summary", summary },
            { "build_succeeded", buildSucceeded },
            { "cancel_source", cancelSource ?? string.Empty },
            { "changed_files", _runState.ChangedFiles.ToArray() },
            { "stop_point", _runState.StopPoint ?? string.Empty },
            { "degraded_flags", _runState.DegradedFlags }
        }, durationMs: (long)_runState.DurationMs);

        PersistRunArtifacts();
    }

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

    #region Global Execution Trace

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
        if (decision.RequiresApproval && decision.ApprovalProposal is not null)
        {
            _approvalRequiredActions.Add(decision.ApprovalProposal);
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

        private void PruneRuntimeArtifacts()
        {
            try
            {
                PruneRunArtifacts(maxRunsToKeep: 25);
                TrimJsonLinesFile(_catalogFile, maxLines: 200);
                TrimJsonLinesFile(Path.Combine(_memoryDirectory, "failure_memory.jsonl"), maxLines: 200);
                TrimJsonLinesFile(Path.Combine(_memoryDirectory, "success_memory.jsonl"), maxLines: 200);
                TrimTextFile(_traceFile, maxChars: 250_000);
                TrimTextFile(_timelineFile, maxChars: 250_000);
            }
            catch
            {
                // Silent fail
            }
        }

        private void PruneRunArtifacts(int maxRunsToKeep)
        {
            if (!Directory.Exists(_runsDirectory))
                return;

            var manifestFiles = Directory.GetFiles(_runsDirectory, "*.manifest.json")
                .Where(path =>
                {
                    var name = Path.GetFileName(path);
                    return !name.StartsWith("latest_", StringComparison.OrdinalIgnoreCase);
                })
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();

            foreach (var staleManifest in manifestFiles.Skip(maxRunsToKeep))
            {
                var runPrefix = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(staleManifest.Name));
                TryDeleteIfExists(staleManifest.FullName);
                TryDeleteIfExists(Path.Combine(_runsDirectory, $"{runPrefix}.events.jsonl"));
                TryDeleteIfExists(Path.Combine(_runsDirectory, $"{runPrefix}.summary.json"));
                TryDeleteIfExists(Path.Combine(_runsDirectory, $"{runPrefix}.summary.txt"));
                TryDeleteIfExists(Path.Combine(_runsDirectory, $"{runPrefix}.timeline.log"));
            }
        }

        private static void TrimJsonLinesFile(string path, int maxLines)
        {
            if (!File.Exists(path))
                return;

            var lines = File.ReadAllLines(path);
            if (lines.Length <= maxLines)
                return;

            File.WriteAllLines(path, lines.Skip(lines.Length - maxLines));
        }

        private static void TrimTextFile(string path, int maxChars)
        {
            if (!File.Exists(path))
                return;

            var content = File.ReadAllText(path);
            if (content.Length <= maxChars)
                return;

            File.WriteAllText(path, content[^maxChars..]);
        }

        private static void TryDeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static string BuildReadableRunSummary(RunManifest manifest)
        {
            var changedFiles = manifest.ChangedFiles.Length == 0
                ? "none"
                : string.Join(Environment.NewLine, manifest.ChangedFiles.Select(path => $"- {path}"));
            var degraded = manifest.DegradedFlags.Count == 0
                ? "none"
                : string.Join(", ", manifest.DegradedFlags.Where(x => x.Value).Select(x => x.Key));

            return string.Join(Environment.NewLine, new[]
            {
                $"Run: {manifest.RunId}",
                $"Status: {manifest.FinalStatus}",
                $"Reason: {manifest.ReasonCode}",
                $"Started: {manifest.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC",
                $"Completed: {manifest.CompletedAtUtc:yyyy-MM-dd HH:mm:ss} UTC",
                $"Duration: {Math.Round(manifest.DurationMs)} ms",
                $"Workspace: {manifest.WorkspaceRoot}",
                $"Access: {manifest.AccessMode}",
                $"Provider/Model: {manifest.Provider} / {manifest.Model}",
                $"Embeddings: {manifest.EmbeddingsStatus}",
                $"Indexing: {manifest.IndexingStatus}",
                $"Build succeeded: {manifest.BuildSucceeded}",
                $"Stop-point: {(string.IsNullOrWhiteSpace(manifest.StopPoint) ? "none" : manifest.StopPoint)}",
                $"Cancel source: {(string.IsNullOrWhiteSpace(manifest.CancelSource) ? "none" : manifest.CancelSource)}",
                $"Degraded flags: {degraded}",
                $"Event count: {manifest.EventCount}",
                "",
                "Task:",
                manifest.TaskNormalized,
                "",
                "Summary:",
                string.IsNullOrWhiteSpace(manifest.Summary) ? "none" : manifest.Summary,
                "",
                "Changed files:",
                changedFiles
            });
        }

        private static string FormatExecutionLogEntry(ExecutionLogEntry entry)
        {
            var details = FlattenMetadata(entry.Details.ToDictionary(x => x.Key, x => (object?)x.Value));
            var detailText = string.IsNullOrWhiteSpace(details) ? string.Empty : $" | {details}";
            var outcome = string.IsNullOrWhiteSpace(entry.Outcome) ? string.Empty : $" | outcome={entry.Outcome}";
            var duration = entry.Duration is null ? string.Empty : $" | duration={Math.Round(entry.Duration.Value)}ms";
            var message = string.IsNullOrWhiteSpace(entry.Message) ? entry.EventType : entry.Message;
            return $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {entry.EventType} | {message}{outcome}{duration}{detailText}";
        }

        private static string FormatActionEvent(ActionEvent entry)
        {
            var metadata = FlattenMetadata(entry.Metadata);
            var reason = string.IsNullOrWhiteSpace(entry.ReasonCode) ? string.Empty : $" | reason={entry.ReasonCode}";
            var duration = entry.DurationMs is null ? string.Empty : $" | duration={entry.DurationMs}ms";
            var correlation = string.IsNullOrWhiteSpace(entry.CorrelationId) ? string.Empty : $" | corr={entry.CorrelationId}";
            var metadataText = string.IsNullOrWhiteSpace(metadata) ? string.Empty : $" | {metadata}";
            return $"[{entry.Sequence:0000}] [{entry.TimestampUtc:HH:mm:ss.fff}] {entry.Component}/{entry.EventType} | {entry.Level} | {entry.Outcome}{reason}{duration}{correlation}{metadataText}";
        }

        private static string FlattenMetadata(IReadOnlyDictionary<string, object?> metadata)
        {
            if (metadata.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            foreach (var pair in metadata.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var formatted = FormatMetadataValue(pair.Value);
                if (!string.IsNullOrWhiteSpace(formatted))
                    parts.Add($"{pair.Key}={formatted}");
            }

            return string.Join(" | ", parts);
        }

        private static string FormatMetadataValue(object? value)
        {
            if (value is null)
                return string.Empty;

            if (value is string text)
            {
                text = text.Replace(Environment.NewLine, " ").Trim();
                if (text.Length > 140)
                    text = text[..137] + "...";
                return text;
            }

            if (value is Array array)
            {
                var items = array.Cast<object?>().Take(3).Select(FormatMetadataValue).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                return array.Length switch
                {
                    0 => "[]",
                    <= 3 => $"[{string.Join(", ", items)}]",
                    _ => $"[{string.Join(", ", items)}, ...] ({array.Length} items)"
                };
            }

            if (value is System.Collections.IDictionary dictionary)
            {
                var items = new List<string>();
                foreach (System.Collections.DictionaryEntry entry in dictionary)
                {
                    if (items.Count == 3)
                        break;
                    items.Add($"{entry.Key}:{FormatMetadataValue(entry.Value)}");
                }

                return dictionary.Count switch
                {
                    0 => "{}",
                    <= 3 => $"{{{string.Join(", ", items)}}}",
                    _ => $"{{{string.Join(", ", items)}, ...}} ({dictionary.Count} keys)"
                };
            }

            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }

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

    #endregion

        #region File-Level Trace

        public void LogFileConsideration(FileTraceEntry entry)
        {
            _fileTraces.Add(entry);
        }

        public void LogFileSelection(string filePath, double finalScore, int rankPosition, string reason)
        {
            var entry = new FileTraceEntry
            {
                Timestamp = DateTime.UtcNow,
                FilePath = filePath,
                State = "Selected",
                FinalScore = finalScore,
                RankPosition = rankPosition,
                Reason = reason,
                SemanticScore = 0,
                SymbolScore = 0,
                StateScore = 0,
                MemoryScore = 0,
                FailureRecords = new List<FailureRecord>(),
                SuccessRecords = new List<SuccessRecord>()
            };

            _fileTraces.Add(entry);
        }

        public void LogFileRejection(string filePath, string reason)
        {
            var entry = new FileTraceEntry
            {
                Timestamp = DateTime.UtcNow,
                FilePath = filePath,
                State = "Rejected",
                Reason = reason,
                FinalScore = 0,
                RankPosition = -1,
                SemanticScore = 0,
                SymbolScore = 0,
                StateScore = 0,
                MemoryScore = 0,
                FailureRecords = new List<FailureRecord>(),
                SuccessRecords = new List<SuccessRecord>()
            };

            _fileTraces.Add(entry);
        }

        #endregion

        #region Scoring Breakdown

        public void LogScoringBreakdown(ScoringBreakdown breakdown)
        {
            _scoringBreakdowns.Add(breakdown);
        }

        #endregion

        #region Memory Influence

        public void LogMemoryInfluence(MemoryInfluence influence)
        {
            _memoryInfluences.Add(influence);
        }

        #endregion

        #region Patch Decision

        public void LogPatchDecision(PatchDecision decision)
        {
            _patchDecisions.Add(decision);
            var summary = $"file={decision.TargetFile}; scope={decision.Scope}; risk={decision.RiskLevel}; reason={decision.Reason}";
            LogEvent("PatchDecision", "Patch scope selected", new Dictionary<string, object>
            {
                { "TargetFile", decision.TargetFile },
                { "TargetMethod", decision.TargetMethod },
                { "Scope", decision.Scope },
                { "Reason", decision.Reason },
                { "RiskLevel", decision.RiskLevel },
                { "Summary", summary },
                { "AlternativeFiles", decision.AlternativeFiles.ToArray() }
            });
        }

        #endregion

        #region Build Result

        public void LogBuildVerificationResult(LocalCursorAgent.Execution.BuildVerifier.BuildResult result)
        {
            var classification = result.Success ? "Success" : (result.Errors.Count > 0 ? "CompilationError" : "Unknown");
            var rootCauseGuess = result.Success
                ? "No build issues"
                : (result.Errors.Count > 0 ? result.Errors[0] : "Build process failed");
            var fixReasoning = result.Success
                ? "Apply changes to source"
                : "Return errors to agent and continue iteration";

            _buildResults.Add(new BuildResult
            {
                Timestamp = DateTime.UtcNow,
                Success = result.Success,
                ErrorClassification = classification,
                RootCauseGuess = rootCauseGuess,
                FixAttemptReasoning = fixReasoning
            });

            LogEvent("BuildVerification", "Build result evaluated", new Dictionary<string, object>
            {
                { "Success", result.Success },
                { "ErrorCount", result.Errors.Count },
                { "WarningCount", result.Warnings.Count },
                { "ErrorClassification", classification },
                { "RootCauseGuess", rootCauseGuess },
                { "FixAttemptReasoning", fixReasoning },
                { "Errors", result.Errors.ToArray() },
                { "Warnings", result.Warnings.ToArray() }
            });
        }

        #endregion

        #region Memory Update

        public void LogMemoryUpdate(MemoryUpdate update)
        {
            _memoryUpdates.Add(update);
        }

        #endregion

        #region Diagnostic Engine

        public DiagnosticSummary GenerateDiagnosticSummary()
        {
            var summary = new DiagnosticSummary
            {
                Timestamp = DateTime.UtcNow,
                TotalFilesConsidered = _fileTraces.Count,
                SelectedFiles = _fileTraces.Count(t => t.State == "Selected"),
                RejectedFiles = _fileTraces.Count(t => t.State == "Rejected"),
                MemoryInfluence = CalculateMemoryInfluence(),
                SymbolSystemEffectiveness = CalculateSymbolEffectiveness(),
                SemanticEffectiveness = CalculateSemanticEffectiveness(),
                ConfidenceScore = CalculateConfidenceScore()
            };

            summary.OptimalDecision = summary.ConfidenceScore > 0.7;
            summary.MemoryImpact = DetermineMemoryImpact();
            summary.SymbolImpact = DetermineSymbolImpact();
            summary.SemanticImpact = DetermineSemanticImpact();

            return summary;
        }

        private double CalculateMemoryInfluence()
        {
            if (_memoryInfluences.Count == 0) return 0;

            double totalInfluence = 0;
            foreach (var influence in _memoryInfluences)
            {
                totalInfluence += influence.TotalMemoryContribution;
            }

            return totalInfluence / _memoryInfluences.Count;
        }

        private double CalculateSymbolEffectiveness()
        {
            int symbolMatches = _fileTraces.Count(t => t.SymbolScore > 0.1);
            return (double)symbolMatches / Math.Max(_fileTraces.Count, 1);
        }

        private double CalculateSemanticEffectiveness()
        {
            int semanticMatches = _fileTraces.Count(t => t.SemanticScore > 0.1);
            return (double)semanticMatches / Math.Max(_fileTraces.Count, 1);
        }

        private double CalculateConfidenceScore()
        {
            int highScoreFiles = _fileTraces.Count(t => t.FinalScore > 0.8);
            return (double)highScoreFiles / Math.Max(_fileTraces.Count, 1);
        }

        private string DetermineMemoryImpact()
        {
            double avgMemory = CalculateMemoryInfluence();
            if (avgMemory > 0.3) return "High positive impact";
            if (avgMemory > 0.1) return "Moderate positive impact";
            if (avgMemory < -0.1) return "Negative impact";
            return "Neutral impact";
        }

        private string DetermineSymbolImpact()
        {
            double effectiveness = CalculateSymbolEffectiveness();
            if (effectiveness > 0.7) return "High positive impact";
            if (effectiveness > 0.3) return "Moderate positive impact";
            return "Low impact";
        }

        private string DetermineSemanticImpact()
        {
            double effectiveness = CalculateSemanticEffectiveness();
            if (effectiveness > 0.7) return "High positive impact";
            if (effectiveness > 0.3) return "Moderate positive impact";
            return "Low impact";
        }

        #endregion

        #region Recommendations Engine

        public List<string> GenerateRecommendations()
        {
            var recommendations = new List<string>();

            var summary = GenerateDiagnosticSummary();

            if (summary.MemoryInfluence > 0.3)
                recommendations.Add("Memory influence too strong, reduce weight to 0.15");

            if (summary.SymbolImpact == "Low impact")
                recommendations.Add("Symbol match underused, increase boost to +0.25");

            if (summary.SemanticImpact == "Low impact")
                recommendations.Add("Semantic retrieval selecting too many irrelevant files");

            if (summary.TotalFilesConsidered > 20)
                recommendations.Add("Context size too large, reduce max files to 10");

            if (summary.RejectedFiles > summary.SelectedFiles)
                recommendations.Add("Too many files rejected, consider relaxing filters");

            return recommendations;
        }

        #endregion

        #region Snapshot Generation

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
                    AccessModeDescription = DescribeAccessMode(snapshot.SessionHeader?.AccessMode ?? string.Empty),
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

        #endregion

        #region Data Models

        public class ExecutionLogEntry
        {
            public DateTime Timestamp { get; set; }
            public string EventType { get; set; } = string.Empty;
            public string? Message { get; set; }
            public string? Outcome { get; set; }
            public double? Duration { get; set; }
            public Dictionary<string, object> Details { get; set; } = new();
        }

        public class FileTraceEntry
        {
            public DateTime Timestamp { get; set; }
            public string FilePath { get; set; } = string.Empty;
            public string State { get; set; } = string.Empty; // Selected / Rejected
            public double FinalScore { get; set; }
            public int RankPosition { get; set; }
            public string Reason { get; set; } = string.Empty;
            public double SemanticScore { get; set; }
            public double SymbolScore { get; set; }
            public double StateScore { get; set; }
            public double MemoryScore { get; set; }
            public List<FailureRecord> FailureRecords { get; set; } = new();
            public List<SuccessRecord> SuccessRecords { get; set; } = new();
        }

        public class ScoringBreakdown
        {
            public DateTime Timestamp { get; set; }
            public string FilePath { get; set; } = string.Empty;
            public double SemanticScore { get; set; }
            public double SymbolScore { get; set; }
            public double StateScore { get; set; }
            public double MemoryScore { get; set; }
            public double FinalScore { get; set; }
            public double SemanticWeight { get; set; }
            public double SymbolWeight { get; set; }
            public double StateWeight { get; set; }
            public double MemoryWeight { get; set; }
            public string DominantComponent { get; set; } = string.Empty;
        }

        public class MemoryInfluence
        {
            public DateTime Timestamp { get; set; }
            public string FilePath { get; set; } = string.Empty;
            public List<FailureRecord> AppliedFailures { get; set; } = new();
            public List<SuccessRecord> AppliedSuccesses { get; set; } = new();
            public double TotalMemoryContribution { get; set; }
            public double DecayFactor { get; set; }
            public string TaskTypeMatch { get; set; } = string.Empty;
        }

        public class PatchDecision
        {
            public DateTime Timestamp { get; set; }
            public string TargetFile { get; set; } = string.Empty;
            public string TargetMethod { get; set; } = string.Empty;
            public string Scope { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public string RiskLevel { get; set; } = string.Empty;
            public List<string> AlternativeFiles { get; set; } = new();
        }

        public class BuildResult
        {
            public DateTime Timestamp { get; set; }
            public bool Success { get; set; }
            public string ErrorClassification { get; set; } = string.Empty;
            public string RootCauseGuess { get; set; } = string.Empty;
            public string FixAttemptReasoning { get; set; } = string.Empty;
        }

        public class MemoryUpdate
        {
            public DateTime Timestamp { get; set; }
            public List<FailureRecord> NewFailures { get; set; } = new();
            public List<SuccessRecord> NewSuccesses { get; set; } = new();
            public Dictionary<string, double> UpdatedWeights { get; set; } = new();
            public List<string> AffectedFiles { get; set; } = new();
        }

        public class SessionHeader
        {
            public string SessionId { get; set; } = string.Empty;
            public string RuntimeRoot { get; set; } = string.Empty;
            public string WorkspaceRoot { get; set; } = string.Empty;
            public string AccessMode { get; set; } = string.Empty;
            public string[] ProtectedRoots { get; set; } = Array.Empty<string>();
        }

        public class WorkspaceResolutionSnapshot
        {
            public string SeedPath { get; set; } = string.Empty;
            public string RuntimeRoot { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string Reason { get; set; } = string.Empty;
            public string ReasonCode { get; set; } = string.Empty;
            public string ReasonCodeName { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string WorkspaceRoot { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
        }

        public class SessionManifest
        {
            public DateTime GeneratedAtUtc { get; set; }
            public string SnapshotFile { get; set; } = string.Empty;
            public bool WorkspaceResolutionSuccess { get; set; }
            public string WorkspaceResolutionReason { get; set; } = string.Empty;
            public string WorkspaceResolutionReasonCode { get; set; } = string.Empty;
            public string WorkspaceResolutionReasonCodeName { get; set; } = string.Empty;
            public WorkspaceResolutionSnapshot? WorkspaceResolution { get; set; }
            public string SessionId { get; set; } = string.Empty;
            public string RuntimeRoot { get; set; } = string.Empty;
            public string WorkspaceRoot { get; set; } = string.Empty;
            public string AccessMode { get; set; } = string.Empty;
            public string AccessModeDescription { get; set; } = string.Empty;
            public int ProtectedRootsCount { get; set; }
            public SessionHeader? SessionHeader { get; set; }
            public string Query { get; set; } = string.Empty;
            public string Outcome { get; set; } = string.Empty;
            public double DurationMs { get; set; }
            public string RunId { get; set; } = string.Empty;
            public string TaskNormalized { get; set; } = string.Empty;
            public string Provider { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public string EmbeddingsStatus { get; set; } = string.Empty;
            public string IndexingStatus { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public string ReasonCode { get; set; } = string.Empty;
            public string[] ChangedFiles { get; set; } = Array.Empty<string>();
            public bool BuildSucceeded { get; set; }
            public string CancelSource { get; set; } = string.Empty;
            public Dictionary<string, bool> DegradedFlags { get; set; } = new();
            public string StopPoint { get; set; } = string.Empty;
            public string EventStreamFile { get; set; } = string.Empty;
            public string SummaryFile { get; set; } = string.Empty;
            public int EventCount { get; set; }
        }

        public sealed class RunManifest
        {
            public string RunId { get; set; } = string.Empty;
            public string SessionId { get; set; } = string.Empty;
            public DateTime StartedAtUtc { get; set; }
            public DateTime CompletedAtUtc { get; set; }
            public double DurationMs { get; set; }
            public string WorkspaceRoot { get; set; } = string.Empty;
            public string RuntimeRoot { get; set; } = string.Empty;
            public string AccessMode { get; set; } = string.Empty;
            public string TaskRaw { get; set; } = string.Empty;
            public string TaskNormalized { get; set; } = string.Empty;
            public string Provider { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public string EmbeddingsStatus { get; set; } = string.Empty;
            public string IndexingStatus { get; set; } = string.Empty;
            public string FinalStatus { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public string ReasonCode { get; set; } = string.Empty;
            public string[] ChangedFiles { get; set; } = Array.Empty<string>();
            public bool BuildSucceeded { get; set; }
            public string CancelSource { get; set; } = string.Empty;
            public Dictionary<string, bool> DegradedFlags { get; set; } = new();
            public string StopPoint { get; set; } = string.Empty;
            public int EventCount { get; set; }
            public long LastEventSequence { get; set; }
            public string EventStreamFile { get; set; } = string.Empty;
            public string SummaryFile { get; set; } = string.Empty;
        }

        public sealed class RunSummaryArtifact
        {
            public string RunId { get; set; } = string.Empty;
            public string FinalStatus { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public string ReasonCode { get; set; } = string.Empty;
            public DateTime StartedAtUtc { get; set; }
            public DateTime CompletedAtUtc { get; set; }
            public double DurationMs { get; set; }
            public string[] ChangedFiles { get; set; } = Array.Empty<string>();
        }

        public sealed class ActionEvent
        {
            public string RunId { get; set; } = string.Empty;
            public string SessionId { get; set; } = string.Empty;
            public long Sequence { get; set; }
            public DateTime TimestampUtc { get; set; }
            public string EventType { get; set; } = string.Empty;
            public string Component { get; set; } = string.Empty;
            public string Level { get; set; } = string.Empty;
            public string Outcome { get; set; } = string.Empty;
            public string ReasonCode { get; set; } = string.Empty;
            public long? DurationMs { get; set; }
            public string CorrelationId { get; set; } = string.Empty;
            public Dictionary<string, object?> Metadata { get; set; } = new();
        }

        public sealed class RunState
        {
            public string RunId { get; set; } = string.Empty;
            public string SessionId { get; set; } = string.Empty;
            public DateTime StartedAtUtc { get; set; }
            public DateTime CompletedAtUtc { get; set; }
            public double DurationMs { get; set; }
            public string WorkspaceRoot { get; set; } = string.Empty;
            public string RuntimeRoot { get; set; } = string.Empty;
            public string AccessMode { get; set; } = string.Empty;
            public string TaskRaw { get; set; } = string.Empty;
            public string TaskNormalized { get; set; } = string.Empty;
            public string Provider { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public string EmbeddingsStatus { get; set; } = "unknown";
            public string IndexingStatus { get; set; } = "unknown";
            public string FinalStatus { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public string ReasonCode { get; set; } = string.Empty;
            public List<string> ChangedFiles { get; set; } = new();
            public bool BuildSucceeded { get; set; }
            public string CancelSource { get; set; } = string.Empty;
            public Dictionary<string, bool> DegradedFlags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public string? StopPoint { get; set; }
        }

        private static string DescribeAccessMode(string accessMode) => accessMode switch
        {
            nameof(AgentAccessMode.ReadOnly) => "Analysis only; no writes or destructive actions.",
            nameof(AgentAccessMode.WorkspaceWrite) => "Write/patch allowed inside workspace; destructive actions denied.",
            nameof(AgentAccessMode.WorkspaceFullAccess) => "Full engineering access inside workspace; runtime and protected paths remain denied.",
            _ => "Unknown access mode."
        };

public class DiagnosticSummary
{
    public DateTime Timestamp { get; set; }
    public int TotalFilesConsidered { get; set; }
    public int SelectedFiles { get; set; }
    public int RejectedFiles { get; set; }
    public double MemoryInfluence { get; set; }
    public double ConfidenceScore { get; set; }
    public string MemoryImpact { get; set; } = string.Empty;
    public string SymbolImpact { get; set; } = string.Empty;
    public string SemanticImpact { get; set; } = string.Empty;
    public bool OptimalDecision { get; set; }
    public double SymbolSystemEffectiveness { get; set; }
    public double SemanticEffectiveness { get; set; }
}

        public class ExecutionSnapshot
        {
            public DateTime Timestamp { get; set; }
            public string Query { get; set; } = string.Empty;
            public TimeSpan Duration { get; set; }
            public string Outcome { get; set; } = string.Empty;
            public List<ExecutionLogEntry> ExecutionLog { get; set; } = new();
            public List<FileTraceEntry> FileTraces { get; set; } = new();
            public List<ScoringBreakdown> ScoringBreakdowns { get; set; } = new();
            public List<MemoryInfluence> MemoryInfluences { get; set; } = new();
            public List<PatchDecision> PatchDecisions { get; set; } = new();
            public List<BuildResult> BuildResults { get; set; } = new();
            public List<MemoryUpdate> MemoryUpdates { get; set; } = new();
            public SessionHeader? SessionHeader { get; set; }
            public WorkspaceResolutionSnapshot? WorkspaceResolution { get; set; }
            public DiagnosticSummary DiagnosticSummary { get; set; } = new();
            public List<string> Recommendations { get; set; } = new();
        }

        public enum ActionLogLevel
        {
            Trace,
            Debug,
            Info,
            Warning,
            Error
        }

        private sealed class TimedActionScope : IDisposable
        {
            private readonly ExecutionTracer _tracer;
            private readonly string _eventType;
            private readonly string _component;
            private readonly ActionLogLevel _level;
            private readonly Dictionary<string, object?>? _metadata;
            private readonly string? _correlationId;
            private readonly DateTime _startedUtc;
            private bool _disposed;

            public TimedActionScope(
                ExecutionTracer tracer,
                string eventType,
                string component,
                ActionLogLevel level,
                Dictionary<string, object?>? metadata,
                string? correlationId)
            {
                _tracer = tracer;
                _eventType = eventType;
                _component = component;
                _level = level;
                _metadata = metadata;
                _correlationId = correlationId;
                _startedUtc = DateTime.UtcNow;
                _tracer.LogActionEvent($"{eventType}Started", component, level, "started", metadata: metadata, correlationId: correlationId);
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _tracer.LogActionEvent(
                    $"{_eventType}Completed",
                    _component,
                    _level,
                    "completed",
                    metadata: _metadata,
                    durationMs: (long)(DateTime.UtcNow - _startedUtc).TotalMilliseconds,
                    correlationId: _correlationId);
            }
        }

        #endregion
    }
}

public sealed class DestructiveTraceRecord
{
    public string OperationKind { get; init; } = string.Empty;
    public string Step { get; init; } = string.Empty;
    public string OriginalPath { get; init; } = string.Empty;
    public string? TargetPath { get; init; }
    public string? SnapshotPath { get; init; }
    public bool PreviewAccepted { get; init; }
    public bool ApplySucceeded { get; init; }
    public bool ApplyFailed { get; init; }
    public bool RollbackSucceeded { get; init; }
    public bool RollbackFailed { get; init; }
    public bool CommitSucceeded { get; init; }
    public bool CommitFailed { get; init; }
    public string? ReasonCode { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public int StepOrder { get; init; }
}

public sealed class PatchTraceRecord
{
    public string OperationKind { get; init; } = "Patch";
    public string Step { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public string? SnapshotHashBeforeApply { get; init; }
    public bool PreviewAccepted { get; init; }
    public bool ApplySucceeded { get; init; }
    public bool ApplyFailed { get; init; }
    public bool RollbackSucceeded { get; init; }
    public bool RollbackFailed { get; init; }
    public string? ReasonCode { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public int StepOrder { get; init; }
}
