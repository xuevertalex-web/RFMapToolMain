using System.Text.Json;

namespace LocalCursorAgent.Memory;

public sealed class RunStrategyFeedbackWriter
{
    private readonly string _memoryDirectory;
    private readonly string _failureMemoryFile;
    private readonly string _successMemoryFile;
    private readonly string _strategyFeedbackFile;

    public RunStrategyFeedbackWriter(string runtimeRoot)
    {
        var normalizedRuntimeRoot = Path.GetFullPath(runtimeRoot ?? throw new ArgumentNullException(nameof(runtimeRoot)));
        _memoryDirectory = Path.Combine(normalizedRuntimeRoot, "logs", "machine", "memory");
        _failureMemoryFile = Path.Combine(_memoryDirectory, "failure_memory.jsonl");
        _successMemoryFile = Path.Combine(_memoryDirectory, "success_memory.jsonl");
        _strategyFeedbackFile = Path.Combine(_memoryDirectory, "strategy_feedback.json");
        Directory.CreateDirectory(_memoryDirectory);
    }

    public StrategyFeedbackResult Rebuild()
    {
        var failures = ReadRecords(_failureMemoryFile);
        var successes = ReadRecords(_successMemoryFile);

        var profile = new StrategyFeedbackProfile
        {
            GeneratedAtUtc = DateTime.UtcNow,
            TotalFailureRecords = failures.Count,
            TotalSuccessRecords = successes.Count,
            StrictTargetingBias = ComputeStrictTargetingBias(failures),
            SingleFileBias = ComputeSingleFileBias(failures, successes),
            EarlyStopBias = ComputeEarlyStopBias(failures),
            DominantFailureClasses = failures
                .GroupBy(x => x.OutcomeClass, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => $"{g.Key}:{g.Count()}")
                .ToArray()
        };

        File.WriteAllText(_strategyFeedbackFile, JsonSerializer.Serialize(profile, JsonOptions(true)));
        return new StrategyFeedbackResult
        {
            Written = true,
            Profile = profile
        };
    }

    public StrategyFeedbackProfile Load()
    {
        if (!File.Exists(_strategyFeedbackFile))
            return StrategyFeedbackProfile.Empty;

        return JsonSerializer.Deserialize<StrategyFeedbackProfile>(File.ReadAllText(_strategyFeedbackFile), JsonOptions()) ?? StrategyFeedbackProfile.Empty;
    }

    private static double ComputeStrictTargetingBias(IEnumerable<RunLearningRecord> failures)
    {
        var relevant = failures.Count(x => string.Equals(x.OutcomeClass, "WRONG_TARGET", StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(x.OutcomeClass, "AMBIGUITY_REJECTED", StringComparison.OrdinalIgnoreCase));
        return ClampBias(relevant * 0.15);
    }

    private static double ComputeSingleFileBias(IEnumerable<RunLearningRecord> failures, IEnumerable<RunLearningRecord> successes)
    {
        var failureWeight = failures.Count(x => string.Equals(x.OutcomeClass, "BUILD_FAILED_AFTER_PATCH", StringComparison.OrdinalIgnoreCase)) * 0.15;
        var successWeight = successes.Count(x => x.ChangedFiles.Length <= 1) * 0.05;
        return ClampBias(failureWeight + successWeight);
    }

    private static double ComputeEarlyStopBias(IEnumerable<RunLearningRecord> failures)
    {
        var relevant = failures.Count(x => string.Equals(x.OutcomeClass, "SAFE_REJECTION", StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(x.OutcomeClass, "CANCELLED_RUN", StringComparison.OrdinalIgnoreCase));
        return ClampBias(relevant * 0.12);
    }

    private static double ClampBias(double value) => Math.Round(Math.Max(0, Math.Min(1, value)), 2);

    private static List<RunLearningRecord> ReadRecords(string path)
    {
        if (!File.Exists(path))
            return new List<RunLearningRecord>();

        return File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<RunLearningRecord>(line, JsonOptions()))
            .Where(x => x != null)
            .Cast<RunLearningRecord>()
            .ToList();
    }

    private static JsonSerializerOptions JsonOptions(bool writeIndented = false) => new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = writeIndented
    };
}

public sealed class StrategyFeedbackResult
{
    public bool Written { get; init; }
    public StrategyFeedbackProfile Profile { get; init; } = StrategyFeedbackProfile.Empty;
}

public sealed class StrategyFeedbackProfile
{
    public static StrategyFeedbackProfile Empty { get; } = new();

    public DateTime GeneratedAtUtc { get; init; }
    public int TotalFailureRecords { get; init; }
    public int TotalSuccessRecords { get; init; }
    public double StrictTargetingBias { get; init; }
    public double SingleFileBias { get; init; }
    public double EarlyStopBias { get; init; }
    public string[] DominantFailureClasses { get; init; } = Array.Empty<string>();
}
