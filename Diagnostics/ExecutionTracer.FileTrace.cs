using System.Collections.Generic;
using LocalCursorAgent.Memory;

namespace LocalCursorAgent.Diagnostics
{
    public partial class ExecutionTracer
    {
        #region File-Level Trace

        public void LogFileConsideration(FileTraceEntry entry)
        {
            _fileTraces.Add(entry);
        }

        public void LogFileSelection(string filePath, double finalScore, int rankPosition, string reason)
        {
            var entry = new FileTraceEntry
            {
                Timestamp = DateTime.UtcNow,
                FilePath = filePath,
                State = "Selected",
                FinalScore = finalScore,
                RankPosition = rankPosition,
                Reason = reason,
                SemanticScore = 0,
                SymbolScore = 0,
                StateScore = 0,
                MemoryScore = 0,
                FailureRecords = new List<FailureRecord>(),
                SuccessRecords = new List<SuccessRecord>()
            };

            _fileTraces.Add(entry);
        }

        public void LogFileRejection(string filePath, string reason)
        {
            var entry = new FileTraceEntry
            {
                Timestamp = DateTime.UtcNow,
                FilePath = filePath,
                State = "Rejected",
                Reason = reason,
                FinalScore = 0,
                RankPosition = -1,
                SemanticScore = 0,
                SymbolScore = 0,
                StateScore = 0,
                MemoryScore = 0,
                FailureRecords = new List<FailureRecord>(),
                SuccessRecords = new List<SuccessRecord>()
            };

            _fileTraces.Add(entry);
        }

        #endregion
    }
}
