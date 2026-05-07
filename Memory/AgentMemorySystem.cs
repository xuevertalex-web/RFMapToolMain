using System;
using System.Collections.Generic;
using System.Linq;

namespace LocalCursorAgent.Memory
{
    /// <summary>
    /// Производственная система памяти обратной связи агента.
    /// Структурированная память опыта, детерминированная корректировка скоринга, контролируемая система угасания.
    /// </summary>
    public class AgentMemorySystem
    {
        private readonly List<FailureRecord> _failureRecords = new();
        private readonly List<SuccessRecord> _successRecords = new();
        private readonly Dictionary<string, TaskTypeProfile> _taskProfiles = new();

        private readonly MemoryDecaySettings _decaySettings;
        private readonly MemoryScoringWeights _scoringWeights;

        public AgentMemorySystem()
        {
            _decaySettings = MemoryDecaySettings.Default;
            _scoringWeights = MemoryScoringWeights.Default;
        }

        #region Failure Memory

        public void RecordFailure(FailureRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            record.Timestamp = DateTime.UtcNow;
            MemoryRecordProvenance.Ensure(record);
            MemoryRecordConfidence.Ensure(record);

            if (MemoryRecordGovernance.IsConsecutiveDuplicate(record, _failureRecords.LastOrDefault()))
                return;

            _failureRecords.Add(record);
            MemoryRecordGovernance.TrimFailureRecords(_failureRecords);

            UpdateTaskProfileOnFailure(record);
        }

        public IEnumerable<FailureRecord> GetRelevantFailures(string query, int maxResults = 5)
        {
            ApplyDecay();
            var projectScope = MemoryProjectScopeResolver.Resolve(query);

            var scored = _failureRecords
                .Where(f => string.Equals(f.ProjectScope ?? "default", projectScope, StringComparison.Ordinal))
                .Select(f => new { Record = f, Score = CalculateFailureRelevance(f, query) })
                .Where(x => x.Score > 0.1)
                .OrderByDescending(x => x.Score)
                .Take(maxResults)
                .Select(x => x.Record);

            return scored.ToList().AsReadOnly();
        }

        private double CalculateFailureRelevance(FailureRecord record, string query)
        {
            double baseScore = GetSeverityMultiplier(record.Severity);
            
            // Детерминированная проверка совпадения токенов (не эмбеддинги)
            int matchingTokens = CountMatchingTokens(record.Query, query);
            double querySimilarity = (double)matchingTokens / Math.Max(record.Query.Split().Length, query.Split().Length);
            
            double ageFactor = CalculateDecayFactor(record.Timestamp);
            
            return baseScore * querySimilarity * ageFactor * _scoringWeights.FailureRelevanceWeight;
        }

        #endregion

        #region Success Memory

        public void RecordSuccess(SuccessRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            record.Timestamp = DateTime.UtcNow;
            MemoryRecordProvenance.Ensure(record);
            MemoryRecordConfidence.Ensure(record);

            if (MemoryRecordGovernance.IsConsecutiveDuplicate(record, _successRecords.LastOrDefault()))
                return;

            _successRecords.Add(record);
            MemoryRecordGovernance.TrimSuccessRecords(_successRecords);

            UpdateTaskProfileOnSuccess(record);
        }

        public IEnumerable<SuccessRecord> GetRelevantSuccesses(string query, int maxResults = 5)
        {
            ApplyDecay();
            var projectScope = MemoryProjectScopeResolver.Resolve(query);

            var scored = _successRecords
                .Where(s => string.Equals(s.ProjectScope ?? "default", projectScope, StringComparison.Ordinal))
                .Select(s => new { Record = s, Score = CalculateSuccessRelevance(s, query) })
                .Where(x => x.Score > 0.1)
                .OrderByDescending(x => x.Score)
                .Take(maxResults)
                .Select(x => x.Record);

            return scored.ToList().AsReadOnly();
        }

        private double CalculateSuccessRelevance(SuccessRecord record, string query)
        {
            int matchingTokens = CountMatchingTokens(record.Query, query);
            double querySimilarity = (double)matchingTokens / Math.Max(record.Query.Split().Length, query.Split().Length);
            double ageFactor = CalculateDecayFactor(record.Timestamp);
            
            return querySimilarity * ageFactor * _scoringWeights.SuccessRelevanceWeight;
        }

        #endregion

        #region Decay Engine

        private void ApplyDecay()
        {
            var cutoffTime = DateTime.UtcNow - _decaySettings.MaximumRecordAge;
            
            _failureRecords.RemoveAll(f => f.Timestamp < cutoffTime);
            _successRecords.RemoveAll(s => s.Timestamp < cutoffTime);

            // Угасание веса профилей задач
            foreach (var profile in _taskProfiles.Values)
            {
                profile.Decay(_decaySettings.ProfileDecayRate);
            }
        }

        /// <summary>
        /// Точная формула угасания памяти: DecayFactor = exp(-DaysSince / 14)
        /// 14 дней → 50% эффекта, 30 дней → ~12% эффекта
        /// </summary>
        private double CalculateDecayFactor(DateTime timestamp)
        {
            double daysSince = (DateTime.UtcNow - timestamp).TotalDays;
            return Math.Exp(-daysSince / 14.0);
        }

        #endregion

        #region Task Type Profiles

        public TaskTypeProfile GetOrCreateProfile(string taskType)
        {
            if (!_taskProfiles.TryGetValue(taskType, out var profile))
            {
                profile = new TaskTypeProfile(taskType);
                _taskProfiles[taskType] = profile;
            }

            return profile;
        }

        public string GetTaskProfileSummary(string query)
        {
            var taskType = DetectTaskType(query);
            if (!_taskProfiles.TryGetValue(taskType, out var profile))
                return string.Empty;

            var topFailureTypes = profile.FailureDistribution
                .OrderByDescending(x => x.Value)
                .Take(3)
                .Select(x => $"{x.Key}:{x.Value}")
                .ToList();

            var topFailures = profile.FailureReasonDistribution
                .OrderByDescending(x => x.Value)
                .Take(3)
                .Select(x => $"{x.Key}:{x.Value}")
                .ToList();

            if (topFailures.Count == 0 && profile.SuccessCount == 0)
                return string.Empty;

            var recommendedBehavior = BuildRecommendedBehavior(profile);
            var avoidPatterns = BuildAvoidPatterns(profile);

            return $"TaskType={profile.TaskType}; Attempts={profile.TotalAttempts}; SuccessRate={profile.SuccessRate:F2}; TopFailureTypes={string.Join(", ", topFailureTypes)}; TopFailureReasons={string.Join(", ", topFailures)}; RecommendedBehavior={recommendedBehavior}; AvoidPatterns={avoidPatterns}";
        }

        public TaskTypeProfile? GetTaskProfile(string query)
        {
            var taskType = DetectTaskType(query);
            return _taskProfiles.TryGetValue(taskType, out var profile) ? profile : null;
        }

        private static string BuildRecommendedBehavior(TaskTypeProfile profile)
        {
            if (profile.FailureReasonDistribution.ContainsKey("TargetResolutionSafeFailure"))
                return "prioritize exact target resolution before patching";
            if (profile.FailureReasonDistribution.ContainsKey("BuildVerificationFailed"))
                return "tighten patch scope and verify compilation early";
            if (profile.FailureReasonDistribution.ContainsKey("SandboxCreationFailed"))
                return "validate environment/setup before tool execution";
            if (profile.SuccessRate < 0.5)
                return "reduce scope and prefer minimal deterministic changes";
            return "continue with deterministic minimal patching";
        }

        private static string BuildAvoidPatterns(TaskTypeProfile profile)
        {
            var patterns = new List<string>();

            if (profile.FailureReasonDistribution.ContainsKey("TargetResolutionSafeFailure"))
                patterns.Add("avoid semantic fallback for symbol-like targets");
            if (profile.FailureReasonDistribution.ContainsKey("BuildVerificationFailed"))
                patterns.Add("avoid broad rewrites without build checks");
            if (profile.FailureReasonDistribution.ContainsKey("UnhandledException"))
                patterns.Add("avoid unguarded tool execution");

            return patterns.Count > 0 ? string.Join(", ", patterns) : "no special avoid patterns";
        }

        private void UpdateTaskProfileOnFailure(FailureRecord record)
        {
            var profile = GetOrCreateProfile(DetectTaskType(record.Query));
            profile.RecordFailure(record.FailureType, record.Severity, record.Reason);
        }

        private void UpdateTaskProfileOnSuccess(SuccessRecord record)
        {
            var profile = GetOrCreateProfile(DetectTaskType(record.Query));
            profile.RecordSuccess();
        }

        private string DetectTaskType(string query)
        {
            // Детерминированное определение типа задачи по ключевым словам
            if (query.Contains("fix", StringComparison.OrdinalIgnoreCase) || 
                query.Contains("bug", StringComparison.OrdinalIgnoreCase))
                return "BugFix";
            
            if (query.Contains("implement", StringComparison.OrdinalIgnoreCase) || 
                query.Contains("feature", StringComparison.OrdinalIgnoreCase))
                return "FeatureImplementation";
            
            if (query.Contains("refactor", StringComparison.OrdinalIgnoreCase))
                return "Refactoring";
            
            if (query.Contains("test", StringComparison.OrdinalIgnoreCase))
                return "Testing";

            return "General";
        }

        #endregion

        #region Scoring Engine

        public double AdjustContextScore(string filePath, string query, double originalScore)
        {
            var failures = GetRelevantFailures(query, 10).ToList();
            
            // Штраф за файлы которые ранее приводили к ошибкам
            double penalty = 0;
            foreach (var failure in failures)
            {
                if (failure.SelectedFiles.Contains(filePath, StringComparer.Ordinal))
                {
                    penalty += GetSeverityPenalty(failure.Severity) * GetReasonMultiplier(failure.Reason) * CalculateDecayFactor(failure.Timestamp);
                }
            }

            var successes = GetRelevantSuccesses(query, 10).ToList();
            
            // Бонус за файлы которые ранее работали успешно
            double bonus = 0;
            foreach (var success in successes)
            {
                if (success.SelectedFiles.Contains(filePath, StringComparer.Ordinal))
                {
                    bonus += _scoringWeights.SuccessBonus * CalculateDecayFactor(success.Timestamp);
                }
            }

            return originalScore * (1.0 + bonus - Math.Min(penalty, 0.8));
        }

        private double GetSeverityMultiplier(FailureSeverity severity)
        {
            return severity switch
            {
                FailureSeverity.Low => 0.2,
                FailureSeverity.Medium => 0.5,
                FailureSeverity.High => 0.8,
                FailureSeverity.Critical => 1.0,
                _ => 0.3
            };
        }

        private double GetSeverityPenalty(FailureSeverity severity)
        {
            return severity switch
            {
                FailureSeverity.Low => 0.05,
                FailureSeverity.Medium => 0.15,
                FailureSeverity.High => 0.3,
                FailureSeverity.Critical => 0.6,
                _ => 0.1
            };
        }

        private double GetReasonMultiplier(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return 1.0;

            return reason switch
            {
                "BuildVerificationFailed" => 1.35,
                "TargetResolutionSafeFailure" => 0.25,
                "SandboxCreationFailed" => 0.9,
                "UnhandledException" => 1.1,
                _ => 1.0
            };
        }

        #endregion

        #region Helpers

        private static int CountMatchingTokens(string a, string b)
        {
            var tokensA = a.Split(new[] {' ', '.', ',', ';', '(', ')', '[', ']'}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLowerInvariant())
                .ToHashSet();
            
            var tokensB = b.Split(new[] {' ', '.', ',', ';', '(', ')', '[', ']'}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLowerInvariant());

            return tokensB.Count(tokensA.Contains);
        }

        public void ClearAll()
        {
            _failureRecords.Clear();
            _successRecords.Clear();
            _taskProfiles.Clear();
        }

        public int FailureCount => _failureRecords.Count;
        public int SuccessCount => _successRecords.Count;
        public int ProfileCount => _taskProfiles.Count;

        #endregion
    }

    #region Data Models

    public class FailureRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; }
        public string? Source { get; set; }
        public string? ProjectScope { get; set; }
        public double? ConfidenceScore { get; set; }
        public string Query { get; set; } = string.Empty;
        public List<string> SelectedFiles { get; set; } = new();
        public string PatchSummary { get; set; } = string.Empty;
        public string BuildError { get; set; } = string.Empty;
        public FailureType FailureType { get; set; }
        public FailureSeverity Severity { get; set; }
        public string? Reason { get; set; }
    }

    public class SuccessRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; }
        public string? Source { get; set; }
        public string? ProjectScope { get; set; }
        public double? ConfidenceScore { get; set; }
        public string Query { get; set; } = string.Empty;
        public List<string> SelectedFiles { get; set; } = new();
        public List<string> SymbolMatches { get; set; } = new();
        public int ContextSize { get; set; }
        public string PatchType { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
    }

    public class TaskTypeProfile
    {
        public string TaskType { get; }
        
        public int TotalAttempts { get; private set; }
        public int SuccessCount { get; private set; }
        public Dictionary<FailureType, int> FailureDistribution { get; } = new();
        public Dictionary<string, int> FailureReasonDistribution { get; } = new(StringComparer.OrdinalIgnoreCase);
        
        public double SuccessRate => TotalAttempts > 0 ? (double)SuccessCount / TotalAttempts : 0.5;

        public TaskTypeProfile(string taskType)
        {
            TaskType = taskType;
        }

        public void RecordSuccess()
        {
            TotalAttempts++;
            SuccessCount++;
        }

        public void RecordFailure(FailureType failureType, FailureSeverity severity, string? reason)
        {
            TotalAttempts++;
            
            if (!FailureDistribution.ContainsKey(failureType))
                FailureDistribution[failureType] = 0;
            
            FailureDistribution[failureType]++;

            if (!string.IsNullOrWhiteSpace(reason))
            {
                if (!FailureReasonDistribution.ContainsKey(reason))
                    FailureReasonDistribution[reason] = 0;

                FailureReasonDistribution[reason]++;
            }
        }

        public void Decay(double decayRate)
        {
            // Экспоненциальное угасание статистики для предотвращения застоя
            SuccessCount = (int)Math.Round(SuccessCount * (1 - decayRate));
            TotalAttempts = (int)Math.Round(TotalAttempts * (1 - decayRate));
            
            foreach (var key in FailureDistribution.Keys.ToList())
            {
                FailureDistribution[key] = (int)Math.Round(FailureDistribution[key] * (1 - decayRate));
            }

            foreach (var key in FailureReasonDistribution.Keys.ToList())
            {
                FailureReasonDistribution[key] = (int)Math.Round(FailureReasonDistribution[key] * (1 - decayRate));
            }
        }
    }

    #endregion

    #region Enums

    public enum FailureType
    {
        CompilationError,
        WrongFileSelection,
        PatchScopeError,
        SymbolMismatch,
        ContextMismatch,
        Unknown
    }

    public enum FailureSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion

    #region Configuration

    public class MemoryDecaySettings
    {
        public static MemoryDecaySettings Default => new()
        {
            MaximumRecordAge = TimeSpan.FromDays(30),
            HighRelevanceWindow = TimeSpan.FromHours(2),
            MediumRelevanceWindow = TimeSpan.FromDays(1),
            LowRelevanceWindow = TimeSpan.FromDays(7),
            ProfileDecayRate = 0.01
        };

        public TimeSpan MaximumRecordAge { get; init; }
        public TimeSpan HighRelevanceWindow { get; init; }
        public TimeSpan MediumRelevanceWindow { get; init; }
        public TimeSpan LowRelevanceWindow { get; init; }
        public double ProfileDecayRate { get; init; }
    }

    public class MemoryScoringWeights
    {
        public static MemoryScoringWeights Default => new()
        {
            FailureRelevanceWeight = 1.2,
            SuccessRelevanceWeight = 0.8,
            SuccessBonus = 0.15
        };

        public double FailureRelevanceWeight { get; init; }
        public double SuccessRelevanceWeight { get; init; }
        public double SuccessBonus { get; init; }
    }

    #endregion
}
