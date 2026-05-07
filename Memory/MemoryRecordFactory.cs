namespace LocalCursorAgent.Memory
{
    public static class MemoryRecordFactory
    {
        public static FailureRecord CreateFailure(
            string query,
            FailureType failureType,
            FailureSeverity severity,
            string? reason = null,
            string? projectScope = null,
            string? source = null,
            double? confidenceScore = null)
        {
            return new FailureRecord
            {
                Query = query,
                FailureType = failureType,
                Severity = severity,
                Reason = reason,
                ProjectScope = projectScope,
                Source = source,
                ConfidenceScore = confidenceScore
            };
        }

        public static SuccessRecord CreateSuccess(
            string query,
            string? projectScope = null,
            string? source = null,
            double? confidenceScore = null)
        {
            return new SuccessRecord
            {
                Query = query,
                ProjectScope = projectScope,
                Source = source,
                ConfidenceScore = confidenceScore
            };
        }
    }
}
