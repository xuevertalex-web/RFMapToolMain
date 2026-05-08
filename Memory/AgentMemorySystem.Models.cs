namespace LocalCursorAgent.Memory
{
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
            // Exponential decay of profile counters.
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
            ProfileDecayRate = MemoryGovernanceDefaults.ProfileDecayRate
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
