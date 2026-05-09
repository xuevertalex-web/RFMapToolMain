using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Indexing;

namespace LocalCursorAgent.Core;

public enum TargetResolutionGateOutcome
{
    Resolved,
    Bypassed,
    Failed
}

public sealed class TargetResolutionGateResult
{
    public TargetResolutionGateOutcome Outcome { get; init; }
    public string RawTargetToken { get; init; } = string.Empty;
    public string Classification { get; init; } = "Unknown";
    public string ReasonCode { get; init; } = TargetResolutionReasonCodes.TargetNotApplicable;
    public string Reason { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public IReadOnlyList<string> ExactSymbolCandidates { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ExactFilenameCandidates { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PartialCandidates { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SemanticCandidates { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SelectedFiles { get; init; } = Array.Empty<string>();

    public bool IsResolved => Outcome == TargetResolutionGateOutcome.Resolved;
    public bool IsBypassed => Outcome == TargetResolutionGateOutcome.Bypassed;
    public bool IsFailed => Outcome == TargetResolutionGateOutcome.Failed;
}

public sealed class TargetResolutionGate
{
    private readonly ProjectIndexer _projectIndexer;
    private readonly ExecutionTracer _tracer;

    public TargetResolutionGate(ProjectIndexer projectIndexer, ExecutionTracer tracer)
    {
        _projectIndexer = projectIndexer;
        _tracer = tracer;
    }

    public async Task<TargetResolutionGateResult> ResolveAsync(string query)
    {
        _tracer.LogActionEvent("TargetResolution", "TargetResolutionGate", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
        {
            { "query", query }
        });
        var rawToken = ExtractTargetToken(query);
        var classification = ClassifyToken(query, rawToken);
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return LogAndReturn(query, new TargetResolutionGateResult
            {
                Outcome = TargetResolutionGateOutcome.Bypassed,
                RawTargetToken = string.Empty,
                Classification = classification,
                ReasonCode = TargetResolutionReasonCodes.TargetNotApplicable,
                Reason = "No exact target token detected; semantic retrieval may proceed.",
                Confidence = 0.0
            }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        }

        var indexedFiles = _projectIndexer.GetIndexedFiles().ToList();
        if (indexedFiles.Count == 0)
        {
            indexedFiles = await _projectIndexer.FindRelevantFiles(rawToken, 15);
        }

        var exactSymbolCandidates = ResolveExactSymbolCandidates(rawToken, indexedFiles);
        if (exactSymbolCandidates.Count == 1)
        {
            return LogAndReturn(query, new TargetResolutionGateResult
            {
                Outcome = TargetResolutionGateOutcome.Resolved,
                RawTargetToken = rawToken,
                Classification = classification,
                ReasonCode = TargetResolutionReasonCodes.TargetExactSymbolMatch,
                Reason = "Exact symbol match resolved target.",
                Confidence = 1.0,
                ExactSymbolCandidates = exactSymbolCandidates,
                SelectedFiles = exactSymbolCandidates
            }, exactSymbolCandidates, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        }

        if (exactSymbolCandidates.Count > 1)
        {
            return LogAndReturn(query, new TargetResolutionGateResult
            {
                Outcome = TargetResolutionGateOutcome.Failed,
                RawTargetToken = rawToken,
                Classification = classification,
                ReasonCode = TargetResolutionReasonCodes.TargetAmbiguous,
                Reason = "Exact symbol match is ambiguous.",
                Confidence = 0.0,
                ExactSymbolCandidates = exactSymbolCandidates
            }, exactSymbolCandidates, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        }

        var exactFilenameCandidates = ResolveExactFilenameCandidates(rawToken, indexedFiles);
        if (exactFilenameCandidates.Count == 1)
        {
            return LogAndReturn(query, new TargetResolutionGateResult
            {
                Outcome = TargetResolutionGateOutcome.Resolved,
                RawTargetToken = rawToken,
                Classification = classification,
                ReasonCode = TargetResolutionReasonCodes.TargetExactFilenameMatch,
                Reason = "Exact filename match resolved target.",
                Confidence = 1.0,
                ExactFilenameCandidates = exactFilenameCandidates,
                SelectedFiles = exactFilenameCandidates
            }, Array.Empty<string>(), exactFilenameCandidates, Array.Empty<string>(), Array.Empty<string>());
        }

        if (exactFilenameCandidates.Count > 1)
        {
            return LogAndReturn(query, new TargetResolutionGateResult
            {
                Outcome = TargetResolutionGateOutcome.Failed,
                RawTargetToken = rawToken,
                Classification = classification,
                ReasonCode = TargetResolutionReasonCodes.TargetAmbiguous,
                Reason = "Exact filename match is ambiguous.",
                Confidence = 0.0,
                ExactFilenameCandidates = exactFilenameCandidates
            }, Array.Empty<string>(), exactFilenameCandidates, Array.Empty<string>(), Array.Empty<string>());
        }

        var partialCandidates = ResolvePartialCandidates(rawToken, classification, indexedFiles);
        if (partialCandidates.Count == 1)
        {
            return LogAndReturn(query, new TargetResolutionGateResult
            {
                Outcome = TargetResolutionGateOutcome.Resolved,
                RawTargetToken = rawToken,
                Classification = classification,
                ReasonCode = TargetResolutionReasonCodes.TargetPartialMatch,
                Reason = "Constrained partial match resolved target.",
                Confidence = 0.75,
                PartialCandidates = partialCandidates,
                SelectedFiles = partialCandidates
            }, Array.Empty<string>(), Array.Empty<string>(), partialCandidates, Array.Empty<string>());
        }

        if (partialCandidates.Count > 1)
        {
            return LogAndReturn(query, new TargetResolutionGateResult
            {
                Outcome = TargetResolutionGateOutcome.Failed,
                RawTargetToken = rawToken,
                Classification = classification,
                ReasonCode = TargetResolutionReasonCodes.TargetAmbiguous,
                Reason = "Constrained partial match is ambiguous.",
                Confidence = 0.0,
                PartialCandidates = partialCandidates
            }, Array.Empty<string>(), Array.Empty<string>(), partialCandidates, Array.Empty<string>());
        }

        var semanticCandidates = await _projectIndexer.FindRelevantFiles(query, 5);
        if (semanticCandidates.Count == 1)
        {
            return LogAndReturn(query, new TargetResolutionGateResult
            {
                Outcome = TargetResolutionGateOutcome.Failed,
                RawTargetToken = rawToken,
                Classification = classification,
                ReasonCode = TargetResolutionReasonCodes.TargetLowConfidence,
                Reason = "Only semantic retrieval found a candidate; confidence is too low for patching.",
                Confidence = 0.4,
                SemanticCandidates = semanticCandidates
            }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), semanticCandidates);
        }

        if (semanticCandidates.Count > 1)
        {
            return LogAndReturn(query, new TargetResolutionGateResult
            {
                Outcome = TargetResolutionGateOutcome.Failed,
                RawTargetToken = rawToken,
                Classification = classification,
                ReasonCode = TargetResolutionReasonCodes.TargetLowConfidence,
                Reason = "Semantic retrieval is ambiguous; confidence is too low for patching.",
                Confidence = 0.3,
                SemanticCandidates = semanticCandidates
            }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), semanticCandidates);
        }

        return LogAndReturn(query, new TargetResolutionGateResult
        {
            Outcome = TargetResolutionGateOutcome.Failed,
            RawTargetToken = rawToken,
            Classification = classification,
            ReasonCode = TargetTokenHeuristics.IsSymbolLikeToken(rawToken) ? TargetResolutionReasonCodes.TargetSymbolNotFound : TargetResolutionReasonCodes.TargetFileNotFound,
            Reason = TargetTokenHeuristics.IsSymbolLikeToken(rawToken)
                ? "Target symbol not found in workspace."
                : "Target file not found in workspace.",
            Confidence = 0.0,
            SemanticCandidates = semanticCandidates
        }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), semanticCandidates);
    }

    private TargetResolutionGateResult LogAndReturn(
        string query,
        TargetResolutionGateResult result,
        IReadOnlyList<string> exactSymbolCandidates,
        IReadOnlyList<string> exactFilenameCandidates,
        IReadOnlyList<string> partialCandidates,
        IReadOnlyList<string> semanticCandidates)
    {
        _tracer.LogTargetResolutionGate(
            query,
            result.RawTargetToken,
            result.Classification,
            exactSymbolCandidates,
            exactFilenameCandidates,
            partialCandidates,
            semanticCandidates,
            result.SelectedFiles,
            result.Outcome.ToString(),
            result.ReasonCode,
            result.Reason,
            result.Confidence);
        _tracer.LogActionEvent("TargetResolution", "TargetResolutionGate", result.IsFailed ? ExecutionTracer.ActionLogLevel.Warning : ExecutionTracer.ActionLogLevel.Info, result.Outcome.ToString().ToLowerInvariant(), result.ReasonCode, new Dictionary<string, object?>
        {
            { "raw_target_token", result.RawTargetToken },
            { "classification", result.Classification },
            { "selected_files", result.SelectedFiles.ToArray() },
            { "exact_symbol_candidates", exactSymbolCandidates.ToArray() },
            { "exact_filename_candidates", exactFilenameCandidates.ToArray() },
            { "partial_candidates", partialCandidates.ToArray() },
            { "semantic_candidates", semanticCandidates.ToArray() }
        });

        return result;
    }

    private List<string> ResolveExactSymbolCandidates(string rawToken, IReadOnlyList<string> indexedFiles)
    {
        return indexedFiles
            .Where(filePath =>
            {
                var symbols = _projectIndexer.SymbolDirectory.GetSymbols(filePath);
                return symbols.Any(symbol => symbol.Equals(rawToken, StringComparison.OrdinalIgnoreCase));
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> ResolveExactFilenameCandidates(string rawToken, IReadOnlyList<string> indexedFiles)
    {
        return indexedFiles
            .Where(filePath => IsExactFilenameMatch(filePath, rawToken))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> ResolvePartialCandidates(string rawToken, string classification, IReadOnlyList<string> indexedFiles)
    {
        if (!TargetTokenHeuristics.IsMeaningfulPartialToken(rawToken))
            return new List<string>();

        return indexedFiles
            .Where(filePath =>
            {
                var stem = Path.GetFileNameWithoutExtension(filePath);
                if (TargetTokenHeuristics.IsMeaningfulPrefixMatch(stem, rawToken))
                    return true;

                var symbols = _projectIndexer.SymbolDirectory.GetSymbols(filePath);
                return symbols.Any(symbol => TargetTokenHeuristics.IsMeaningfulPrefixMatch(symbol, rawToken));
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsExactFilenameMatch(string filePath, string rawToken)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(rawToken))
            return false;

        var normalizedPath = filePath.Replace('\\', '/');
        var normalizedToken = rawToken.Replace('\\', '/').Trim();
        var fileName = Path.GetFileName(filePath);
        var stem = Path.GetFileNameWithoutExtension(filePath);

        if (normalizedToken.Contains('/'))
        {
            return normalizedPath.EndsWith(normalizedToken, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.Equals(normalizedToken, StringComparison.OrdinalIgnoreCase);
        }

        if (normalizedToken.EndsWith(Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase))
        {
            return fileName.Equals(normalizedToken, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.EndsWith(normalizedToken, StringComparison.OrdinalIgnoreCase);
        }

        return fileName.Equals(normalizedToken, StringComparison.OrdinalIgnoreCase) ||
               stem.Equals(normalizedToken, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractTargetToken(string query) => TargetTokenHeuristics.ExtractTargetToken(query);

    private static string ClassifyToken(string query, string token) => TargetTokenHeuristics.ClassifyToken(query, token);
}
