namespace LocalCursorAgent.Memory
{
    internal static class MemoryRecordConfidence
    {
        public static void Ensure(FailureRecord record)
        {
            if (record.ConfidenceScore is null)
                record.ConfidenceScore = record.Severity switch
                {
                    FailureSeverity.Low => 0.25,
                    FailureSeverity.Medium => 0.5,
                    FailureSeverity.High => 0.75,
                    FailureSeverity.Critical => 0.9,
                    _ => 0.4
                };
        }

        public static void Ensure(SuccessRecord record)
        {
            if (record.ConfidenceScore is null)
                record.ConfidenceScore = 0.7;
        }
    }
}
