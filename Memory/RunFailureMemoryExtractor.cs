using System.Text.Json;
using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Memory;

public sealed class RunFailureMemoryExtractor
{
    private readonly string _runtimeRoot;
    private readonly string _runsDirectory;
    private readonly string _memoryDirectory;
    private readonly string _failureMemoryFile;
    private readonly string _successMemoryFile;
    private readonly string _latestLearningFile;

    public RunFailureMemoryExtractor(string runtimeRoot)
    {
        _runtimeRoot = Path.GetFullPath(runtimeRoot ?? throw new ArgumentNullException(nameof(runtimeRoot)));
        _runsDirectory = Path.Combine(_runtimeRoot, "logs", "machine", "runs");
        _memoryDirectory = Path.Combine(_runtimeRoot, "logs", "machine", "memory");
        _failureMemoryFile = Path.Combine(_memoryDirectory, "failure_memory.jsonl");
        _successMemoryFile = Path.Combine(_memoryDirectory, "success_memory.jsonl");
        _latestLearningFile = Path.Combine(_memoryDirectory, "latest_learning.json");
        Directory.CreateDirectory(_memoryDirectory);
    }

    public LearningCaptureResult CaptureLatestRun()
    {
        var manifestPath = Path.Combine(_runsDirectory, "latest_manifest.json");
        if (!File.Exists(manifestPath))
        {
            return new LearningCaptureResult
            {
                Captured = false,
                Reason = "Latest manifest not found."
            };
        }

        var manifest = JsonSerializer.Deserialize<ExecutionTracer.RunManifest>(File.ReadAllText(manifestPath), JsonOptions());
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.RunId))
        {
            return new LearningCaptureResult
            {
                Captured = false,
                Reason = "Latest manifest could not be parsed."
            };
        }

        var events = ReadEvents(manifest.EventStreamFile);
        var learningRecord = new RunLearningRecord
        {
            RunId = manifest.RunId,
            SessionId = manifest.SessionId,
            StartedAtUtc = manifest.StartedAtUtc,
            CompletedAtUtc = manifest.CompletedAtUtc,
            DurationMs = manifest.DurationMs,
            WorkspaceRoot = manifest.WorkspaceRoot,
            AccessMode = manifest.AccessMode,
            TaskRaw = manifest.TaskRaw,
            TaskNormalized = manifest.TaskNormalized,
            Provider = manifest.Provider,
            Model = manifest.Model,
            FinalStatus = manifest.FinalStatus,
            Summary = manifest.Summary,
            ReasonCode = manifest.ReasonCode,
            StopPoint = manifest.StopPoint,
            BuildSucceeded = manifest.BuildSucceeded,
            ChangedFiles = manifest.ChangedFiles,
            EventCount = manifest.EventCount,
            OutcomeClass = ClassifyOutcome(manifest, events),
            EventTypes = events.Select(e => e.EventType).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            DownstreamAbsence = ExtractDownstreamAbsence(events),
            DegradedFlags = manifest.DegradedFlags ?? new Dictionary<string, bool>(),
            CancelSource = manifest.CancelSource
        };

        var targetFile = string.Equals(manifest.FinalStatus, "succeeded", StringComparison.OrdinalIgnoreCase)
            ? _successMemoryFile
            : _failureMemoryFile;

        AppendJsonLine(targetFile, learningRecord);
        File.WriteAllText(_latestLearningFile, JsonSerializer.Serialize(learningRecord, JsonOptions(true)));

        return new LearningCaptureResult
        {
            Captured = true,
            Reason = learningRecord.OutcomeClass,
            Record = learningRecord
        };
    }

    private List<ExecutionTracer.ActionEvent> ReadEvents(string? eventStreamFile)
    {
        if (string.IsNullOrWhiteSpace(eventStreamFile))
            return new List<ExecutionTracer.ActionEvent>();

        var path = Path.Combine(_runsDirectory, eventStreamFile);
        if (!File.Exists(path))
            return new List<ExecutionTracer.ActionEvent>();

        var events = new List<ExecutionTracer.ActionEvent>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var actionEvent = JsonSerializer.Deserialize<ExecutionTracer.ActionEvent>(line, JsonOptions());
            if (actionEvent != null)
                events.Add(actionEvent);
        }

        return events;
    }

    private static string[] ExtractDownstreamAbsence(IEnumerable<ExecutionTracer.ActionEvent> events)
    {
        var stopPoint = events.FirstOrDefault(e => string.Equals(e.EventType, "StopPoint", StringComparison.OrdinalIgnoreCase));
        if (stopPoint?.Metadata == null || !stopPoint.Metadata.TryGetValue("downstream_absence", out var downstream) || downstream is null)
            return Array.Empty<string>();

        if (downstream is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            return jsonElement.EnumerateArray().Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

        if (downstream is IEnumerable<object?> objectList)
            return objectList.Where(x => x != null).Select(x => x!.ToString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

        return Array.Empty<string>();
    }

    private static string ClassifyOutcome(ExecutionTracer.RunManifest manifest, IReadOnlyList<ExecutionTracer.ActionEvent> events)
    {
        if (string.Equals(manifest.FinalStatus, "cancelled", StringComparison.OrdinalIgnoreCase))
            return "CANCELLED_RUN";

        if (!string.IsNullOrWhiteSpace(manifest.ReasonCode))
        {
            if (manifest.ReasonCode.Contains("WRONG_TARGET", StringComparison.OrdinalIgnoreCase))
                return "WRONG_TARGET";
            if (manifest.ReasonCode.Contains("SAFE_REJECTION", StringComparison.OrdinalIgnoreCase))
                return "SAFE_REJECTION";
            if (manifest.ReasonCode.Contains("AMBIGUITY", StringComparison.OrdinalIgnoreCase))
                return "AMBIGUITY_REJECTED";
            if (manifest.ReasonCode.Contains("BUILD_FAILED_AFTER_PATCH", StringComparison.OrdinalIgnoreCase))
                return "BUILD_FAILED_AFTER_PATCH";
        }

        if (!manifest.BuildSucceeded && manifest.ChangedFiles.Length > 0)
            return "BUILD_FAILED_AFTER_PATCH";

        if (events.Any(e => string.Equals(e.EventType, "StopPoint", StringComparison.OrdinalIgnoreCase)))
            return "SAFE_REJECTION";

        if (!manifest.BuildSucceeded && !string.Equals(manifest.FinalStatus, "succeeded", StringComparison.OrdinalIgnoreCase))
            return "WRONG_SCOPE";

        if ((manifest.DegradedFlags?.TryGetValue("embeddings", out var degraded) ?? false) || string.Equals(manifest.EmbeddingsStatus, "degraded", StringComparison.OrdinalIgnoreCase))
            return "DEGRADED_EMBEDDING";

        return string.Equals(manifest.FinalStatus, "succeeded", StringComparison.OrdinalIgnoreCase)
            ? "SUCCESS"
            : "UNKNOWN_FAILURE";
    }

    private static JsonSerializerOptions JsonOptions(bool writeIndented = false) => new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = writeIndented
    };

    private static void AppendJsonLine<T>(string path, T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions());
        File.AppendAllText(path, json + Environment.NewLine);
    }
}

public sealed class LearningCaptureResult
{
    public bool Captured { get; init; }
    public string Reason { get; init; } = string.Empty;
    public RunLearningRecord? Record { get; init; }
}

public sealed class RunLearningRecord
{
    public string RunId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public DateTime StartedAtUtc { get; init; }
    public DateTime CompletedAtUtc { get; init; }
    public double DurationMs { get; init; }
    public string WorkspaceRoot { get; init; } = string.Empty;
    public string AccessMode { get; init; } = string.Empty;
    public string TaskRaw { get; init; } = string.Empty;
    public string TaskNormalized { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string FinalStatus { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string ReasonCode { get; init; } = string.Empty;
    public string StopPoint { get; init; } = string.Empty;
    public bool BuildSucceeded { get; init; }
    public string[] ChangedFiles { get; init; } = Array.Empty<string>();
    public int EventCount { get; init; }
    public string OutcomeClass { get; init; } = string.Empty;
    public string[] EventTypes { get; init; } = Array.Empty<string>();
    public string[] DownstreamAbsence { get; init; } = Array.Empty<string>();
    public Dictionary<string, bool> DegradedFlags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string CancelSource { get; init; } = string.Empty;
}
