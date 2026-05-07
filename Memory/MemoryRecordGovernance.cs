using System;
using System.Collections.Generic;
using System.Linq;

namespace LocalCursorAgent.Memory
{
    internal static class MemoryRecordGovernance
    {
        public static bool IsConsecutiveDuplicate(FailureRecord candidate, FailureRecord? last)
        {
            if (last == null)
                return false;

            return string.Equals(candidate.Query, last.Query, StringComparison.Ordinal) &&
                   string.Equals(candidate.ProjectScope, last.ProjectScope, StringComparison.Ordinal) &&
                   string.Equals(candidate.Source, last.Source, StringComparison.Ordinal) &&
                   candidate.FailureType == last.FailureType &&
                   candidate.Severity == last.Severity &&
                   Nullable.Equals(candidate.ConfidenceScore, last.ConfidenceScore) &&
                   string.Equals(candidate.Reason, last.Reason, StringComparison.Ordinal) &&
                   string.Equals(candidate.PatchSummary, last.PatchSummary, StringComparison.Ordinal) &&
                   string.Equals(candidate.BuildError, last.BuildError, StringComparison.Ordinal) &&
                   SameFiles(candidate.SelectedFiles, last.SelectedFiles);
        }

        public static bool IsConsecutiveDuplicate(SuccessRecord candidate, SuccessRecord? last)
        {
            if (last == null)
                return false;

            return string.Equals(candidate.Query, last.Query, StringComparison.Ordinal) &&
                   string.Equals(candidate.ProjectScope, last.ProjectScope, StringComparison.Ordinal) &&
                   string.Equals(candidate.Source, last.Source, StringComparison.Ordinal) &&
                   Nullable.Equals(candidate.ConfidenceScore, last.ConfidenceScore) &&
                   string.Equals(candidate.PatchType, last.PatchType, StringComparison.Ordinal) &&
                   string.Equals(candidate.TaskType, last.TaskType, StringComparison.Ordinal) &&
                   candidate.ContextSize == last.ContextSize &&
                   SameFiles(candidate.SelectedFiles, last.SelectedFiles) &&
                   SameFiles(candidate.SymbolMatches, last.SymbolMatches);
        }

        public static void TrimFailureRecords(List<FailureRecord> records)
        {
            TrimToLimit(records, MemoryGovernanceDefaults.MaxFailureRecords);
        }

        public static void TrimSuccessRecords(List<SuccessRecord> records)
        {
            TrimToLimit(records, MemoryGovernanceDefaults.MaxSuccessRecords);
        }

        private static bool SameFiles(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (a.Count != b.Count)
                return false;

            for (var i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static void TrimToLimit<T>(List<T> records, int maxCount)
        {
            var overflow = records.Count - maxCount;
            if (overflow > 0)
            {
                records.RemoveRange(0, overflow);
            }
        }
    }
}
