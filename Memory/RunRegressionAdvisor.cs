using System.Text.Json;

namespace LocalCursorAgent.Memory;

public sealed class RunRegressionAdvisor
{
    private readonly string _failureMemoryFile;
    private readonly RunStrategyFeedbackWriter _strategyFeedbackWriter;

    public RunRegressionAdvisor(string runtimeRoot)
    {
        if (string.IsNullOrWhiteSpace(runtimeRoot))
            throw new ArgumentNullException(nameof(runtimeRoot));

        var normalizedRuntimeRoot = Path.GetFullPath(runtimeRoot);
        _failureMemoryFile = Path.Combine(normalizedRuntimeRoot, "logs", "machine", "memory", "failure_memory.jsonl");
        _strategyFeedbackWriter = new RunStrategyFeedbackWriter(normalizedRuntimeRoot);
    }

    public RegressionAdvice BuildAdvice(string task, int maxItems = 3)
    {
        if (string.IsNullOrWhiteSpace(task) || !File.Exists(_failureMemoryFile))
            return RegressionAdvice.Empty;

        var taskTokens = Tokenize(task);
        if (taskTokens.Count == 0)
            return RegressionAdvice.Empty;

        var records = File.ReadLines(_failureMemoryFile)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<RunLearningRecord>(line, JsonOptions()))
            .Where(record => record != null)
            .Cast<RunLearningRecord>()
            .Select(record => new
            {
                Record = record,
                Score = ComputeScore(record, taskTokens)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Record.CompletedAtUtc)
            .Take(maxItems)
            .Select(x => x.Record)
            .ToList();

        if (records.Count == 0)
            return RegressionAdvice.Empty;

        var lines = new List<string>
        {
            "REGRESSION GUARD:"
        };

        foreach (var record in records)
        {
            lines.Add($"- Avoid repeating {record.OutcomeClass}.");
            if (!string.IsNullOrWhiteSpace(record.StopPoint))
                lines.Add($"- Previous stop-point: {record.StopPoint}.");
            if (!string.IsNullOrWhiteSpace(record.ReasonCode))
                lines.Add($"- Previous reason code: {record.ReasonCode}.");
            if (record.DownstreamAbsence.Length > 0)
                lines.Add($"- If blocked early, do not force downstream steps: {string.Join(", ", record.DownstreamAbsence)}.");
        }

        var shapingLines = BuildPromptShapingLines(records);

        var strategyProfile = _strategyFeedbackWriter.Load();
        var strategyBiasText = BuildStrategyBiasText(strategyProfile);

        return new RegressionAdvice
        {
            HasAdvice = true,
            Text = string.Join(Environment.NewLine, lines),
            PromptShapingText = string.Join(Environment.NewLine, shapingLines),
            StrategyBiasText = strategyBiasText,
            Records = records
        };
    }

    private static List<string> BuildPromptShapingLines(IReadOnlyList<RunLearningRecord> records)
    {
        var lines = new List<string>();

        if (records.Any(r => string.Equals(r.OutcomeClass, "WRONG_TARGET", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(r.OutcomeClass, "AMBIGUITY_REJECTED", StringComparison.OrdinalIgnoreCase)))
        {
            lines.Add("PROMPT SHAPING:");
            lines.Add("- Require exact target confirmation before any mutation tool call.");
            lines.Add("- If exact target is missing or ambiguous, stop safely instead of guessing.");
        }

        if (records.Any(r => string.Equals(r.OutcomeClass, "BUILD_FAILED_AFTER_PATCH", StringComparison.OrdinalIgnoreCase)))
        {
            if (lines.Count == 0)
                lines.Add("PROMPT SHAPING:");
            lines.Add("- Prefer a minimal single-file patch before broader edits.");
            lines.Add("- Re-check build after each concrete mutation.");
        }

        if (records.Any(r => string.Equals(r.OutcomeClass, "SAFE_REJECTION", StringComparison.OrdinalIgnoreCase)))
        {
            if (lines.Count == 0)
                lines.Add("PROMPT SHAPING:");
            lines.Add("- Do not force downstream build or patch steps after an early gate rejection.");
        }

        return lines;
    }

    private static string BuildStrategyBiasText(StrategyFeedbackProfile profile)
    {
        if (profile == null || (profile.StrictTargetingBias <= 0 && profile.SingleFileBias <= 0 && profile.EarlyStopBias <= 0))
            return string.Empty;

        var lines = new List<string>
        {
            "STRATEGY BIAS:"
        };

        if (profile.StrictTargetingBias > 0)
            lines.Add($"- Exact target strictness bias: {profile.StrictTargetingBias:F2}.");
        if (profile.SingleFileBias > 0)
            lines.Add($"- Single-file preference bias: {profile.SingleFileBias:F2}.");
        if (profile.EarlyStopBias > 0)
            lines.Add($"- Early-stop safety bias: {profile.EarlyStopBias:F2}.");
        if (profile.DominantFailureClasses.Length > 0)
            lines.Add($"- Dominant failure memory: {string.Join(", ", profile.DominantFailureClasses)}.");

        return string.Join(Environment.NewLine, lines);
    }

    private static double ComputeScore(RunLearningRecord record, HashSet<string> taskTokens)
    {
        var recordTokens = Tokenize(record.TaskRaw + " " + record.TaskNormalized + " " + record.ReasonCode + " " + record.OutcomeClass);
        if (recordTokens.Count == 0)
            return 0;

        var overlap = recordTokens.Count(taskTokens.Contains);
        if (overlap == 0)
            return 0;

        var normalized = (double)overlap / Math.Max(taskTokens.Count, recordTokens.Count);
        var severityBoost = string.Equals(record.FinalStatus, "failed", StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.25;
        return normalized + severityBoost;
    }

    private static HashSet<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return text
            .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ';', ':', '/', '\\', '(', ')', '[', ']', '{', '}', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 3)
            .Select(token => token.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed class RegressionAdvice
{
    public static RegressionAdvice Empty { get; } = new();

    public bool HasAdvice { get; init; }
    public string Text { get; init; } = string.Empty;
    public string PromptShapingText { get; init; } = string.Empty;
    public string StrategyBiasText { get; init; } = string.Empty;
    public IReadOnlyList<RunLearningRecord> Records { get; init; } = Array.Empty<RunLearningRecord>();
}
