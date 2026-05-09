using System;
using System.Collections.Generic;
using System.Linq;
using LocalCursorAgent.Memory;
using LocalCursorAgent.Security;

namespace LocalCursorAgent.Diagnostics
{
    public partial class ExecutionTracer
    {
        #region Data Models

        public class ExecutionLogEntry
        {
            public DateTime Timestamp { get; set; }
            public string EventType { get; set; } = string.Empty;
            public string? Message { get; set; }
            public string? Outcome { get; set; }
            public double? Duration { get; set; }
            public Dictionary<string, object> Details { get; set; } = new();
        }

        public class FileTraceEntry
        {
            public DateTime Timestamp { get; set; }
            public string FilePath { get; set; } = string.Empty;
            public string State { get; set; } = string.Empty;
            public double FinalScore { get; set; }
            public int RankPosition { get; set; }
            public string Reason { get; set; } = string.Empty;
            public double SemanticScore { get; set; }
            public double SymbolScore { get; set; }
            public double StateScore { get; set; }
            public double MemoryScore { get; set; }
            public List<FailureRecord> FailureRecords { get; set; } = new();
            public List<SuccessRecord> SuccessRecords { get; set; } = new();
        }

        public class ScoringBreakdown
        {
            public DateTime Timestamp { get; set; }
            public string FilePath { get; set; } = string.Empty;
            public double SemanticScore { get; set; }
            public double SymbolScore { get; set; }
            public double StateScore { get; set; }
            public double MemoryScore { get; set; }
            public double FinalScore { get; set; }
            public double SemanticWeight { get; set; }
            public double SymbolWeight { get; set; }
            public double StateWeight { get; set; }
            public double MemoryWeight { get; set; }
            public string DominantComponent { get; set; } = string.Empty;
        }

        public class MemoryInfluence
        {
            public DateTime Timestamp { get; set; }
            public string FilePath { get; set; } = string.Empty;
            public List<FailureRecord> AppliedFailures { get; set; } = new();
            public List<SuccessRecord> AppliedSuccesses { get; set; } = new();
            public double TotalMemoryContribution { get; set; }
            public double DecayFactor { get; set; }
            public string TaskTypeMatch { get; set; } = string.Empty;
        }

        public class PatchDecision
        {
            public DateTime Timestamp { get; set; }
            public string TargetFile { get; set; } = string.Empty;
            public string TargetMethod { get; set; } = string.Empty;
            public string Scope { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public string RiskLevel { get; set; } = string.Empty;
            public List<string> AlternativeFiles { get; set; } = new();
        }

        public class BuildResult
        {
            public DateTime Timestamp { get; set; }
            public bool Success { get; set; }
            public string ErrorClassification { get; set; } = string.Empty;
            public string RootCauseGuess { get; set; } = string.Empty;
            public string FixAttemptReasoning { get; set; } = string.Empty;
        }

        public class MemoryUpdate
        {
            public DateTime Timestamp { get; set; }
            public List<FailureRecord> NewFailures { get; set; } = new();
            public List<SuccessRecord> NewSuccesses { get; set; } = new();
            public Dictionary<string, double> UpdatedWeights { get; set; } = new();
            public List<string> AffectedFiles { get; set; } = new();
        }

        public class SessionHeader
        {
            public string SessionId { get; set; } = string.Empty;
            public string RuntimeRoot { get; set; } = string.Empty;
            public string WorkspaceRoot { get; set; } = string.Empty;
            public string AccessMode { get; set; } = string.Empty;
            public string[] ProtectedRoots { get; set; } = Array.Empty<string>();
        }

        public class WorkspaceResolutionSnapshot
        {
            public string SeedPath { get; set; } = string.Empty;
            public string RuntimeRoot { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string Reason { get; set; } = string.Empty;
            public string ReasonCode { get; set; } = string.Empty;
            public string ReasonCodeName { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string WorkspaceRoot { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
        }

        public class SessionManifest
        {
            public DateTime GeneratedAtUtc { get; set; }
            public string SnapshotFile { get; set; } = string.Empty;
            public bool WorkspaceResolutionSuccess { get; set; }
            public string WorkspaceResolutionReason { get; set; } = string.Empty;
            public string WorkspaceResolutionReasonCode { get; set; } = string.Empty;
            public string WorkspaceResolutionReasonCodeName { get; set; } = string.Empty;
            public WorkspaceResolutionSnapshot? WorkspaceResolution { get; set; }
            public string SessionId { get; set; } = string.Empty;
            public string RuntimeRoot { get; set; } = string.Empty;
            public string WorkspaceRoot { get; set; } = string.Empty;
            public string AccessMode { get; set; } = string.Empty;
            public string AccessModeDescription { get; set; } = string.Empty;
            public int ProtectedRootsCount { get; set; }
            public SessionHeader? SessionHeader { get; set; }
            public string Query { get; set; } = string.Empty;
            public string Outcome { get; set; } = string.Empty;
            public double DurationMs { get; set; }
            public string RunId { get; set; } = string.Empty;
            public string TaskNormalized { get; set; } = string.Empty;
            public string Provider { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public string EmbeddingsStatus { get; set; } = string.Empty;
            public string IndexingStatus { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public string ReasonCode { get; set; } = string.Empty;
            public string[] ChangedFiles { get; set; } = Array.Empty<string>();
            public bool BuildSucceeded { get; set; }
            public string CancelSource { get; set; } = string.Empty;
            public Dictionary<string, bool> DegradedFlags { get; set; } = new();
            public string StopPoint { get; set; } = string.Empty;
            public string EventStreamFile { get; set; } = string.Empty;
            public string SummaryFile { get; set; } = string.Empty;
            public int EventCount { get; set; }
        }

        public sealed class RunManifest
        {
            public string RunId { get; set; } = string.Empty;
            public string SessionId { get; set; } = string.Empty;
            public DateTime StartedAtUtc { get; set; }
            public DateTime CompletedAtUtc { get; set; }
            public double DurationMs { get; set; }
            public string WorkspaceRoot { get; set; } = string.Empty;
            public string RuntimeRoot { get; set; } = string.Empty;
            public string AccessMode { get; set; } = string.Empty;
            public string TaskRaw { get; set; } = string.Empty;
            public string TaskNormalized { get; set; } = string.Empty;
            public string Provider { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public string EmbeddingsStatus { get; set; } = string.Empty;
            public string IndexingStatus { get; set; } = string.Empty;
            public string FinalStatus { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public string ReasonCode { get; set; } = string.Empty;
            public string[] ChangedFiles { get; set; } = Array.Empty<string>();
            public bool BuildSucceeded { get; set; }
            public string CancelSource { get; set; } = string.Empty;
            public Dictionary<string, bool> DegradedFlags { get; set; } = new();
            public string StopPoint { get; set; } = string.Empty;
            public int EventCount { get; set; }
            public long LastEventSequence { get; set; }
            public string EventStreamFile { get; set; } = string.Empty;
            public string SummaryFile { get; set; } = string.Empty;
        }

        public sealed class RunSummaryArtifact
        {
            public string RunId { get; set; } = string.Empty;
            public string FinalStatus { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public string ReasonCode { get; set; } = string.Empty;
            public DateTime StartedAtUtc { get; set; }
            public DateTime CompletedAtUtc { get; set; }
            public double DurationMs { get; set; }
            public string[] ChangedFiles { get; set; } = Array.Empty<string>();
        }

        public sealed class ActionEvent
        {
            public string RunId { get; set; } = string.Empty;
            public string SessionId { get; set; } = string.Empty;
            public long Sequence { get; set; }
            public DateTime TimestampUtc { get; set; }
            public string EventType { get; set; } = string.Empty;
            public string Component { get; set; } = string.Empty;
            public string Level { get; set; } = string.Empty;
            public string Outcome { get; set; } = string.Empty;
            public string ReasonCode { get; set; } = string.Empty;
            public long? DurationMs { get; set; }
            public string CorrelationId { get; set; } = string.Empty;
            public Dictionary<string, object?> Metadata { get; set; } = new();
        }

        public sealed class ModelRetryAttemptDiagnostics
        {
            public int Attempt { get; set; }
            public string Reason { get; set; } = string.Empty;
            public int DelayMs { get; set; }
            public bool WillRetry { get; set; }
            public bool FinalAttempt { get; set; }
        }

        public sealed class RunState
        {
            public string RunId { get; set; } = string.Empty;
            public string SessionId { get; set; } = string.Empty;
            public DateTime StartedAtUtc { get; set; }
            public DateTime CompletedAtUtc { get; set; }
            public double DurationMs { get; set; }
            public string WorkspaceRoot { get; set; } = string.Empty;
            public string RuntimeRoot { get; set; } = string.Empty;
            public string AccessMode { get; set; } = string.Empty;
            public string TaskRaw { get; set; } = string.Empty;
            public string TaskNormalized { get; set; } = string.Empty;
            public string Provider { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public string EmbeddingsStatus { get; set; } = "unknown";
            public string IndexingStatus { get; set; } = "unknown";
            public string FinalStatus { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public string ReasonCode { get; set; } = string.Empty;
            public List<string> ChangedFiles { get; set; } = new();
            public bool BuildSucceeded { get; set; }
            public string CancelSource { get; set; } = string.Empty;
            public Dictionary<string, bool> DegradedFlags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public string? StopPoint { get; set; }
        }

        public class DiagnosticSummary
        {
            public DateTime Timestamp { get; set; }
            public int TotalFilesConsidered { get; set; }
            public int SelectedFiles { get; set; }
            public int RejectedFiles { get; set; }
            public double MemoryInfluence { get; set; }
            public double ConfidenceScore { get; set; }
            public string MemoryImpact { get; set; } = string.Empty;
            public string SymbolImpact { get; set; } = string.Empty;
            public string SemanticImpact { get; set; } = string.Empty;
            public bool OptimalDecision { get; set; }
            public double SymbolSystemEffectiveness { get; set; }
            public double SemanticEffectiveness { get; set; }
        }

        public class ExecutionSnapshot
        {
            public DateTime Timestamp { get; set; }
            public string Query { get; set; } = string.Empty;
            public TimeSpan Duration { get; set; }
            public string Outcome { get; set; } = string.Empty;
            public List<ExecutionLogEntry> ExecutionLog { get; set; } = new();
            public List<FileTraceEntry> FileTraces { get; set; } = new();
            public List<ScoringBreakdown> ScoringBreakdowns { get; set; } = new();
            public List<MemoryInfluence> MemoryInfluences { get; set; } = new();
            public List<PatchDecision> PatchDecisions { get; set; } = new();
            public List<BuildResult> BuildResults { get; set; } = new();
            public List<MemoryUpdate> MemoryUpdates { get; set; } = new();
            public SessionHeader? SessionHeader { get; set; }
            public WorkspaceResolutionSnapshot? WorkspaceResolution { get; set; }
            public DiagnosticSummary DiagnosticSummary { get; set; } = new();
            public List<string> Recommendations { get; set; } = new();
        }

        #endregion
    }
}
