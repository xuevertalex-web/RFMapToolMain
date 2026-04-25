using System.Text.Json;

namespace LocalCursorAgent.Memory;

public sealed class AdaptiveGateTuningPreviewWriter
{
    private readonly string _memoryDirectory;
    private readonly string _previewFile;
    private readonly RunStrategyFeedbackWriter _strategyFeedbackWriter;

    public AdaptiveGateTuningPreviewWriter(string runtimeRoot)
    {
        var normalizedRuntimeRoot = Path.GetFullPath(runtimeRoot ?? throw new ArgumentNullException(nameof(runtimeRoot)));
        _memoryDirectory = Path.Combine(normalizedRuntimeRoot, "logs", "machine", "memory");
        _previewFile = Path.Combine(_memoryDirectory, "adaptive_gate_tuning_preview.json");
        _strategyFeedbackWriter = new RunStrategyFeedbackWriter(normalizedRuntimeRoot);
        Directory.CreateDirectory(_memoryDirectory);
    }

    public AdaptiveGateTuningPreviewResult Rebuild()
    {
        var strategy = _strategyFeedbackWriter.Load();
        var preview = new AdaptiveGateTuningPreview
        {
            GeneratedAtUtc = DateTime.UtcNow,
            PreviewMode = true,
            RecommendedTargetConfidenceFloor = Math.Round(0.75 + strategy.StrictTargetingBias * 0.20, 2),
            RecommendedSemanticFallbackAllowed = strategy.StrictTargetingBias < 0.45,
            RecommendedSingleFilePreference = Math.Round(0.50 + strategy.SingleFileBias * 0.40, 2),
            RecommendedMaxSafeFileCount = strategy.SingleFileBias >= 0.50 ? 1 : strategy.SingleFileBias >= 0.25 ? 2 : 3,
            RecommendedEarlyStopStrictness = Math.Round(0.40 + strategy.EarlyStopBias * 0.40, 2),
            StrategyFeedback = strategy
        };

        File.WriteAllText(_previewFile, JsonSerializer.Serialize(preview, JsonOptions(true)));
        return new AdaptiveGateTuningPreviewResult
        {
            Written = true,
            Preview = preview
        };
    }

    public AdaptiveGateTuningPreview Load()
    {
        if (!File.Exists(_previewFile))
            return AdaptiveGateTuningPreview.Empty;

        return JsonSerializer.Deserialize<AdaptiveGateTuningPreview>(File.ReadAllText(_previewFile), JsonOptions()) ?? AdaptiveGateTuningPreview.Empty;
    }

    private static JsonSerializerOptions JsonOptions(bool writeIndented = false) => new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = writeIndented
    };
}

public sealed class AdaptiveGateTuningPreviewResult
{
    public bool Written { get; init; }
    public AdaptiveGateTuningPreview Preview { get; init; } = AdaptiveGateTuningPreview.Empty;
}

public sealed class AdaptiveGateTuningPreview
{
    public static AdaptiveGateTuningPreview Empty { get; } = new();

    public DateTime GeneratedAtUtc { get; init; }
    public bool PreviewMode { get; init; }
    public double RecommendedTargetConfidenceFloor { get; init; }
    public bool RecommendedSemanticFallbackAllowed { get; init; }
    public double RecommendedSingleFilePreference { get; init; }
    public int RecommendedMaxSafeFileCount { get; init; }
    public double RecommendedEarlyStopStrictness { get; init; }
    public StrategyFeedbackProfile StrategyFeedback { get; init; } = StrategyFeedbackProfile.Empty;
}
