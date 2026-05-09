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
public partial class ExecutionTracer
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
    private readonly List<ActionLifecycleEntry> _actionLedger = new();
    private readonly Dictionary<string, string> _pendingActionCorrelations = new(StringComparer.Ordinal);
    private int _deniedPermissionDecisions;
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
        _approvalRequiredActions.Clear();
        _actionLedger.Clear();
        _pendingActionCorrelations.Clear();
        _deniedPermissionDecisions = 0;

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

    #endregion

}
}

