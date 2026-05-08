using LocalCursorAgent.Memory;

namespace LocalCursorAgent.Context
{
    public enum ContextComplexity
    {
        Low,
        Medium,
        High
    }

    public enum ContextSelectionStrategy
    {
        StrictExact,
        Balanced,
        Exploratory
    }

    public class ContextBudgetPlan
    {
        public ContextComplexity Complexity { get; set; }
        public int Budget { get; set; }
        public int MinFiles { get; set; }
        public int MaxFiles { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ContextInformation
    {
        public List<string> SelectedFiles { get; set; } = new();
        public Dictionary<string, string> FileContents { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> FileStateFlags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> RelevantSymbols { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int TotalLength { get; set; }
        public ContextBudgetPlan BudgetPlan { get; set; } = new();
    }

    public class RankedFileScore
    {
        public string FilePath { get; set; } = string.Empty;
        public int SemanticPosition { get; set; }
        public double SemanticScore { get; set; }
        public int SymbolMatches { get; set; }
        public int StateBoost { get; set; }
        public double RecencyScore { get; set; }
        public int MatchPriority { get; set; }
        public double SortScore { get; set; }
    }

    public class ContextSelection
    {
        public List<ScoredFile> SelectedFiles { get; set; } = new();
        public int TotalConsidered { get; set; }
    }

    public class TargetResolutionReport
    {
        public string Query { get; set; } = string.Empty;
        public string TargetToken { get; set; } = string.Empty;
        public List<string> SymbolCandidates { get; set; } = new();
        public List<string> FilenameCandidates { get; set; } = new();
        public List<string> SelectedFiles { get; set; } = new();
        public bool SafeFailure { get; set; }
        public string? FailureMessage { get; set; }
        public string ResolutionReason { get; set; } = string.Empty;
    }

    public class ScoredFile
    {
        public string FilePath { get; set; } = string.Empty;
        public double SemanticScore { get; set; }
        public double SymbolScore { get; set; }
        public double StateScore { get; set; }
        public double MemoryScore { get; set; }
        public double FinalScore { get; set; }
        public List<FailureRecord> FailureRecords { get; set; } = new();
        public List<SuccessRecord> SuccessRecords { get; set; } = new();
    }
}
