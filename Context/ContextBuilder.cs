using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Security;
using LocalCursorAgent.Tools;
using LocalCursorAgent.Embeddings;
using LocalCursorAgent.Indexing;
using LocalCursorAgent.Memory;

namespace LocalCursorAgent.Context
{
    public class ContextBuilder
    {
        private static readonly object DiagnosticsLock = new();
        private static ContextDiagnosticsSnapshot _latestDiagnostics = new();
        private readonly string _projectPath;
        private readonly VectorStore _vectorStore;
        private readonly FileStateManager _fileStateManager;
        private readonly ProjectSymbolDirectory _symbolDirectory;
        private readonly ExecutionTracer _tracer;
        private readonly AgentMemorySystem _memorySystem;
        private readonly TextFileService _textFileService;

        private const int LOW_COMPLEXITY_MAX_FILES = 6;
        private const int MEDIUM_COMPLEXITY_MAX_FILES = 10;
        private const int HIGH_COMPLEXITY_MAX_FILES = 15;
        private const int LOW_COMPLEXITY_MAX_CHARS = 18000;
        private const int MEDIUM_COMPLEXITY_MAX_CHARS = 30000;
        private const int HIGH_COMPLEXITY_MAX_CHARS = 45000;

        public ContextBuilder(string projectPath, VectorStore vectorStore, FileStateManager fileStateManager, ProjectSymbolDirectory symbolDirectory, ExecutionTracer? tracer = null, AgentMemorySystem? memorySystem = null)
        {
            _projectPath = projectPath;
            _vectorStore = vectorStore;
            _fileStateManager = fileStateManager;
            _symbolDirectory = symbolDirectory;
            _tracer = tracer ?? new ExecutionTracer(AgentRuntimePaths.ResolveRuntimeRoot(AppContext.BaseDirectory));
            _memorySystem = memorySystem ?? new AgentMemorySystem();
            _textFileService = new TextFileService();
        }

        public ExecutionTracer Tracer => _tracer;

        public static ContextDiagnosticsSnapshot GetLatestDiagnostics()
        {
            lock (DiagnosticsLock)
            {
                return _latestDiagnostics.Clone();
            }
        }

        // Backward-compatible constructor for diagnostics-oriented flow.
        public ContextBuilder(ExecutionTracer tracer, AgentMemorySystem memorySystem)
        {
            _projectPath = Directory.GetCurrentDirectory();
            _vectorStore = new VectorStore();
            _fileStateManager = new FileStateManager();
            _symbolDirectory = new ProjectSymbolDirectory();
            _tracer = tracer;
            _memorySystem = memorySystem;
            _textFileService = new TextFileService();
        }

        public ContextBudgetPlan ComputeBudgetPlan(string query, List<string> semanticMatches, List<string> symbolMatches)
        {
            var complexity = DetectComplexity(query, semanticMatches, symbolMatches);
            var budget = complexity switch
            {
                ContextComplexity.High => HIGH_COMPLEXITY_MAX_FILES,
                ContextComplexity.Medium => MEDIUM_COMPLEXITY_MAX_FILES,
                _ => LOW_COMPLEXITY_MAX_FILES
            };

            return new ContextBudgetPlan
            {
                Complexity = complexity,
                Budget = budget,
                MinFiles = Math.Max(3, budget / 2),
                MaxFiles = budget,
                Reason = $"query_tokens={CountTokens(query)}, semantic={semanticMatches.Count}, symbols={symbolMatches.Count}"
            };
        }

        public ContextInformation BuildContext(string query, List<string> semanticMatches, List<string> symbolMatches, int budget)
        {
            if (IsEmptyOrPromptNoise(query))
            {
                _tracer.LogTargetResolution(query, string.Empty, symbolMatches, CollectFilenameCandidates(string.Empty, semanticMatches, symbolMatches), new List<string>(), true, "EmptyTask");
                return new ContextInformation
                {
                    TotalLength = 0,
                    BudgetPlan = new ContextBudgetPlan
                    {
                        Complexity = ContextComplexity.Low,
                        Budget = 0,
                        MinFiles = 0,
                        MaxFiles = 0,
                        Reason = "EmptyTask"
                    }
                };
            }

            var plan = ComputeBudgetPlan(query, semanticMatches, symbolMatches);
            var originalBudget = Math.Max(plan.MinFiles, Math.Min(plan.MaxFiles, budget));
            var effectiveBudget = originalBudget;
            var taskProfile = _memorySystem.GetTaskProfile(query);
            var strategy = DetermineSelectionStrategy(taskProfile);
            var budgetReductionReason = "None";
            if (taskProfile != null)
            {
                if (taskProfile.SuccessRate < 0.5)
                {
                    effectiveBudget = Math.Max(3, effectiveBudget - 2);
                    budgetReductionReason = "LowSuccessRate";
                }
                else if (taskProfile.SuccessRate < 0.75)
                {
                    effectiveBudget = Math.Max(3, effectiveBudget - 1);
                    budgetReductionReason = "MediumSuccessRate";
                }
            }

            if (strategy == ContextSelectionStrategy.StrictExact)
            {
                effectiveBudget = Math.Max(3, Math.Min(effectiveBudget, 3));
                budgetReductionReason = budgetReductionReason == "None" ? "StrictExactStrategy" : $"{budgetReductionReason}+StrictExactStrategy";
            }

            plan.Reason = $"{plan.Reason}; strategy={strategy}; budget={effectiveBudget}; reduction={budgetReductionReason}";

            var targetToken = ExtractTargetToken(query);
            var resolution = ResolveTargetCandidates(targetToken, semanticMatches, symbolMatches, strategy);
            _tracer.LogTargetResolution(query, targetToken, symbolMatches, CollectFilenameCandidates(targetToken, semanticMatches, symbolMatches), resolution.Candidates, resolution.SafeFailure, resolution.Reason);

            if (resolution.SafeFailure)
            {
                return new ContextInformation
                {
                    BudgetPlan = plan,
                    TotalLength = 0
                };
            }

            var candidates = resolution.Candidates;
            var ranked = candidates
                .Select(path => CreateFileScore(path, query, semanticMatches, symbolMatches, targetToken, strategy))
                .OrderByDescending(x => x.MatchPriority)
                .ThenByDescending(x => x.SortScore)
                .ThenBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var maxChars = plan.Complexity switch
            {
                ContextComplexity.High => HIGH_COMPLEXITY_MAX_CHARS,
                ContextComplexity.Medium => MEDIUM_COMPLEXITY_MAX_CHARS,
                _ => LOW_COMPLEXITY_MAX_CHARS
            };

            _tracer.LogEvent("ContextRanking", "Ranked context candidates", new Dictionary<string, object>
            {
                { "Query", query },
                { "TargetToken", targetToken },
                { "OriginalBudget", originalBudget },
                { "Budget", effectiveBudget },
                { "AppliedBudgetReduction", originalBudget - effectiveBudget },
                { "BudgetReductionReason", budgetReductionReason },
                { "TaskProfileSuccessRate", taskProfile?.SuccessRate ?? 0.5 },
                { "SelectionStrategy", strategy.ToString() },
                { "StrategyApplied", true },
                { "ProfileSummary", _memorySystem.GetTaskProfileSummary(query) },
                { "SelectedFiles", ranked.Take(effectiveBudget).Select(x => x.FilePath).ToArray() },
                { "Ranking", ranked.Select(x => $"{x.FilePath}:{x.MatchPriority}:{x.SortScore:F3}:{BuildInclusionReason(x, targetToken)}").ToArray() }
            });

            var rankedWithContent = ranked
                .Take(effectiveBudget)
                .Select(file => new
                {
                    File = file,
                    Content = LoadContextContent(file.FilePath)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Content))
                .ToList();

            var bestPrefixLength = FindBestPrefixLength(rankedWithContent.Select(x => x.Content.Length).ToList(), maxChars);
            var context = new ContextInformation { BudgetPlan = plan };
            var diagnosticsItems = new List<ContextDiagnosticsItem>();
            var selectedPrefix = rankedWithContent.Take(bestPrefixLength);

            foreach (var item in selectedPrefix)
            {
                if (context.TotalLength > 0 && context.TotalLength + item.Content.Length > maxChars)
                    break;

                context.SelectedFiles.Add(item.File.FilePath);
                context.FileStateFlags[item.File.FilePath] = _fileStateManager.GetStateFlag(item.File.FilePath) ?? "(Clean)";
                context.RelevantSymbols[item.File.FilePath] = _symbolDirectory.GetSymbols(item.File.FilePath);
                context.FileContents[item.File.FilePath] = item.Content;
                context.TotalLength += item.Content.Length;
                diagnosticsItems.Add(new ContextDiagnosticsItem
                {
                    Path = item.File.FilePath,
                    Reason = BuildInclusionReason(item.File, targetToken),
                    Priority = item.File.MatchPriority,
                    CharCount = item.Content.Length
                });
            }
            lock (DiagnosticsLock)
            {
                _latestDiagnostics = new ContextDiagnosticsSnapshot
                {
                    Items = diagnosticsItems,
                    TotalFiles = diagnosticsItems.Count,
                    TotalChars = context.TotalLength,
                    BudgetUsed = diagnosticsItems.Count,
                    BudgetLimit = effectiveBudget
                };
            }
            return context;
        }

        public TargetResolutionReport ResolveTarget(string query, List<string> semanticMatches, List<string> symbolMatches)
        {
            var targetToken = ExtractTargetToken(query);
            var resolution = ResolveTargetCandidates(targetToken, semanticMatches, symbolMatches, ContextSelectionStrategy.Balanced);
            return new TargetResolutionReport
            {
                Query = query,
                TargetToken = targetToken,
                SymbolCandidates = symbolMatches.ToList(),
                FilenameCandidates = CollectFilenameCandidates(targetToken, semanticMatches, symbolMatches),
                SelectedFiles = resolution.Candidates,
                SafeFailure = resolution.SafeFailure,
                FailureMessage = resolution.SafeFailure ? resolution.Reason : null,
                ResolutionReason = resolution.Reason
            };
        }

        public bool TryResolveTarget(string query, List<string> semanticMatches, List<string> symbolMatches, out List<string> candidates, out string? failureMessage)
        {
            if (IsEmptyOrPromptNoise(query))
            {
                candidates = new List<string>();
                failureMessage = "EmptyTask";
                _tracer.LogTargetResolution(query, string.Empty, symbolMatches, CollectFilenameCandidates(string.Empty, semanticMatches, symbolMatches), candidates, true, failureMessage);
                return false;
            }

            var targetToken = ExtractTargetToken(query);
            var resolution = ResolveTargetCandidates(targetToken, semanticMatches, symbolMatches, ContextSelectionStrategy.Balanced);
            candidates = resolution.Candidates;
            failureMessage = resolution.SafeFailure ? resolution.Reason : null;
            _tracer.LogTargetResolution(query, targetToken, symbolMatches, CollectFilenameCandidates(targetToken, semanticMatches, symbolMatches), candidates, resolution.SafeFailure, failureMessage);
            return !resolution.SafeFailure;
        }

        public string FormatContext(ContextInformation context)
        {
            var sb = new StringBuilder();

            foreach (var file in context.SelectedFiles)
            {
                var state = context.FileStateFlags.TryGetValue(file, out var flag) ? flag : "(Clean)";
                sb.AppendLine($"// FILE: {file} {state}");

                if (context.RelevantSymbols.TryGetValue(file, out var symbols) && symbols.Count > 0)
                {
                    sb.AppendLine($"// SYMBOLS: {string.Join(", ", symbols.Take(8))}");
                }

                if (context.FileContents.TryGetValue(file, out var content) && !string.IsNullOrWhiteSpace(content))
                {
                    sb.AppendLine(content);
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        public async Task<ContextSelection> BuildContextAsync(string query, List<string> availableFiles)
        {
            _tracer.LogExecutionStart(query, DateTime.UtcNow);

            var selection = new ContextSelection();
            try
            {
                var info = BuildContext(query, availableFiles, new List<string>(), MEDIUM_COMPLEXITY_MAX_FILES);
                selection.SelectedFiles = info.SelectedFiles
                    .Select((f, idx) => new ScoredFile
                    {
                        FilePath = f,
                        SemanticScore = Math.Max(0, 1.0 - idx * 0.05),
                        SymbolScore = CountQuerySymbolMatches(query, _symbolDirectory.GetSymbols(f)),
                        StateScore = _fileStateManager.GetStateBoost(f),
                        MemoryScore = _memorySystem.AdjustContextScore(f, query, 1.0) - 1.0,
                        FinalScore = Math.Max(0, 1.0 - idx * 0.05)
                    })
                    .ToList();
                selection.TotalConsidered = availableFiles.Count;

                _tracer.LogEvent("ContextSelection", "Context selected", new Dictionary<string, object>
                {
                    { "SelectedCount", selection.SelectedFiles.Count },
                    { "TotalConsidered", selection.TotalConsidered }
                });

                _tracer.GenerateExecutionSnapshot(query, TimeSpan.FromMilliseconds(_tracer.GetExecutionDuration()), "Success");
                await Task.CompletedTask;
                return selection;
            }
            catch
            {
                _tracer.LogExecutionEnd(DateTime.UtcNow, TimeSpan.FromMilliseconds(_tracer.GetExecutionDuration()), "Failed");
                throw;
            }
        }

        private ContextComplexity DetectComplexity(string query, List<string> semanticMatches, List<string> symbolMatches)
        {
            var tokens = CountTokens(query);
            var totalCandidates = semanticMatches.Count + symbolMatches.Count;

            if (tokens >= 14 || totalCandidates >= 20)
                return ContextComplexity.High;
            if (tokens >= 8 || totalCandidates >= 10)
                return ContextComplexity.Medium;
            return ContextComplexity.Low;
        }

        private static int CountTokens(string query)
        {
            return query.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static List<string> BuildCandidatePool(List<string> semanticMatches, List<string> symbolMatches)
        {
            return semanticMatches
                .Concat(symbolMatches)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string ExtractTargetToken(string query)
        {
            var tokens = query.Split(new[] { ' ', '\t', '\r', '\n', ':', ',', '.', '(', ')', '[', ']', '{', '}', '"', '\'' }, StringSplitOptions.RemoveEmptyEntries);
            return tokens.FirstOrDefault(t => t.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
                                              t.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
                                              t.EndsWith("Manager", StringComparison.OrdinalIgnoreCase) ||
                                              t.EndsWith("Handler", StringComparison.OrdinalIgnoreCase) ||
                                              t.EndsWith("Repository", StringComparison.OrdinalIgnoreCase))
                   ?? string.Empty;
        }

        private (List<string> Candidates, bool SafeFailure, string Reason) ResolveTargetCandidates(string targetToken, List<string> semanticMatches, List<string> symbolMatches, ContextSelectionStrategy strategy)
        {
            if (string.IsNullOrWhiteSpace(targetToken))
            {
                return (BuildCandidatePool(semanticMatches, symbolMatches), false, "SemanticSelection");
            }

            var isSymbolLikeTarget = IsSymbolLikeTarget(targetToken);

            var exactSymbolMatches = symbolMatches
                .Where(path => Path.GetFileNameWithoutExtension(path).Equals(targetToken, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (exactSymbolMatches.Count > 0)
                return (exactSymbolMatches, false, "ExactSymbolMatch");

            var exactFilenameMatches = semanticMatches
                .Concat(symbolMatches)
                .Where(path => Path.GetFileNameWithoutExtension(path).Equals(targetToken, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (exactFilenameMatches.Count > 0)
                return (exactFilenameMatches, false, "ExactFilenameMatch");

            if (isSymbolLikeTarget || strategy == ContextSelectionStrategy.StrictExact)
                return (new List<string>(), true, "Target symbol not found in workspace");

            var partialSymbolMatches = symbolMatches
                .Where(path => Path.GetFileNameWithoutExtension(path).IndexOf(targetToken, StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (partialSymbolMatches.Count > 0)
                return (partialSymbolMatches, false, "PartialSymbolMatch");

            if (strategy == ContextSelectionStrategy.Balanced)
                return (new List<string>(), true, "Target symbol not found in workspace");

            var semanticFallback = semanticMatches
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (semanticFallback.Count > 0)
                return (semanticFallback, false, "SemanticFallback");

            return (new List<string>(), true, "Target symbol not found in workspace");
        }

        private static bool IsSymbolLikeTarget(string targetToken)
        {
            return targetToken.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
                   targetToken.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
                   targetToken.EndsWith("Manager", StringComparison.OrdinalIgnoreCase) ||
                   targetToken.EndsWith("Handler", StringComparison.OrdinalIgnoreCase) ||
                   targetToken.EndsWith("Repository", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEmptyOrPromptNoise(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            var trimmed = query.Trim();
            return trimmed.Equals("Task:", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.Equals("Task", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.Length < 5;
        }

        private List<string> CollectFilenameCandidates(string targetToken, List<string> semanticMatches, List<string> symbolMatches)
        {
            if (string.IsNullOrWhiteSpace(targetToken))
                return new List<string>();

            return semanticMatches
                .Concat(symbolMatches)
                .Where(path => Path.GetFileNameWithoutExtension(path).IndexOf(targetToken, StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private RankedFileScore CreateFileScore(string filePath, string query, List<string> semanticMatches, List<string> symbolMatches, string targetToken, ContextSelectionStrategy strategy)
        {
            var semanticPosition = semanticMatches.FindIndex(x => x.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            var semanticScore = semanticPosition >= 0 ? 1.0 / (semanticPosition + 1) : 0.0;

            var querySymbolMatches = CountQuerySymbolMatches(query, _symbolDirectory.GetSymbols(filePath));
            var symbolScore = symbolMatches.Contains(filePath, StringComparer.OrdinalIgnoreCase) ? 0.5 + querySymbolMatches : querySymbolMatches;

            var stateBoost = _fileStateManager.GetStateBoost(filePath);
            var recencyScore = ConvertRecencyToScore(_fileStateManager.GetLastActivityUtc(filePath));
            var matchPriority = GetTargetMatchPriority(filePath, targetToken);
            var weights = GetStrategyWeights(strategy);
            var sortScore = semanticScore * weights.Semantic + symbolScore * weights.Symbol + stateBoost * weights.State + recencyScore * weights.Recency;

            return new RankedFileScore
            {
                FilePath = filePath,
                SemanticPosition = semanticPosition,
                SemanticScore = semanticScore,
                SymbolMatches = querySymbolMatches,
                StateBoost = stateBoost,
                RecencyScore = recencyScore,
                MatchPriority = matchPriority,
                SortScore = sortScore + matchPriority * 100.0
            };
        }

        private static int GetTargetMatchPriority(string filePath, string targetToken)
        {
            if (string.IsNullOrWhiteSpace(targetToken))
                return 0;

            var fileName = Path.GetFileNameWithoutExtension(filePath);

            if (fileName.Equals(targetToken, StringComparison.OrdinalIgnoreCase))
                return 3;

            if (fileName.StartsWith(targetToken, StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(targetToken, StringComparison.OrdinalIgnoreCase))
                return 2;

            if (fileName.IndexOf(targetToken, StringComparison.OrdinalIgnoreCase) >= 0)
                return 1;

            return 0;
        }

        private static string BuildInclusionReason(RankedFileScore score, string targetToken)
        {
            if (!string.IsNullOrWhiteSpace(targetToken) && score.MatchPriority >= 3) return "exact_path_match";
            if (!string.IsNullOrWhiteSpace(targetToken) && score.MatchPriority == 2) return "same_directory_or_prefix";
            if (score.SymbolMatches > 0) return "symbol_match";
            return "fallback";
        }

        private ContextSelectionStrategy DetermineSelectionStrategy(TaskTypeProfile? taskProfile)
        {
            if (taskProfile == null)
                return ContextSelectionStrategy.Balanced;

            if (taskProfile.SuccessRate < 0.5)
                return ContextSelectionStrategy.StrictExact;

            if (taskProfile.SuccessRate < 0.75)
                return ContextSelectionStrategy.Balanced;

            return ContextSelectionStrategy.Exploratory;
        }

        private static (double Semantic, double Symbol, double State, double Recency) GetStrategyWeights(ContextSelectionStrategy strategy)
        {
            return strategy switch
            {
                ContextSelectionStrategy.StrictExact => (0.45, 0.35, 0.15, 0.05),
                ContextSelectionStrategy.Exploratory => (0.65, 0.20, 0.10, 0.05),
                _ => (0.55, 0.30, 0.10, 0.05)
            };
        }

        private static int CountQuerySymbolMatches(string query, List<string> fileSymbols)
        {
            return SymbolIndexer.CountMatchingSymbols(query, fileSymbols);
        }

        private static double ConvertRecencyToScore(DateTime timestamp)
        {
            if (timestamp == DateTime.MinValue)
                return 0;

            var age = DateTime.UtcNow - timestamp;
            if (age <= TimeSpan.FromMinutes(10)) return 1.0;
            if (age <= TimeSpan.FromHours(1)) return 0.6;
            if (age <= TimeSpan.FromHours(24)) return 0.3;
            return 0.1;
        }

        private string LoadContextContent(string filePath)
        {
            var absolutePath = Path.Combine(_projectPath, filePath);
            return File.Exists(absolutePath)
                ? _textFileService.Read(absolutePath).NormalizedText
                : _vectorStore.GetMetadata(filePath) ?? string.Empty;
        }

        private static int FindBestPrefixLength(List<int> lengths, int maxChars)
        {
            if (lengths.Count == 0 || maxChars <= 0)
                return 0;

            var prefixSums = new int[lengths.Count + 1];
            for (var i = 0; i < lengths.Count; i++)
            {
                checked
                {
                    prefixSums[i + 1] = prefixSums[i] + Math.Max(0, lengths[i]);
                }
            }

            var lo = 0;
            var hi = lengths.Count;
            var best = 0;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) / 2);
                if (prefixSums[mid] <= maxChars)
                {
                    best = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return best;
        }
    }

    public sealed class ContextDiagnosticsItem
    {
        public string Path { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public int Priority { get; set; }
        public int CharCount { get; set; }
    }

    public sealed class ContextDiagnosticsSnapshot
    {
        public List<ContextDiagnosticsItem> Items { get; set; } = new();
        public int TotalFiles { get; set; }
        public int TotalChars { get; set; }
        public int BudgetUsed { get; set; }
        public int BudgetLimit { get; set; }

        public ContextDiagnosticsSnapshot Clone()
        {
            return new ContextDiagnosticsSnapshot
            {
                Items = Items.Select(x => new ContextDiagnosticsItem
                {
                    Path = x.Path,
                    Reason = x.Reason,
                    Priority = x.Priority,
                    CharCount = x.CharCount
                }).ToList(),
                TotalFiles = TotalFiles,
                TotalChars = TotalChars,
                BudgetUsed = BudgetUsed,
                BudgetLimit = BudgetLimit
            };
        }
    }

}
