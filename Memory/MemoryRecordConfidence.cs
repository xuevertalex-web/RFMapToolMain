namespace LocalCursorAgent.Memory
{
    internal static class MemoryRecordConfidence
    {
        public static void Ensure(FailureRecord record)
        {
            if (record.ConfidenceScore is null)
                record.ConfidenceScore = record.Severity switch
                {
                    FailureSeverity.Low => MemoryGovernanceDefaults.DefaultFailureLowConfidence,
                    FailureSeverity.Medium => MemoryGovernanceDefaults.DefaultFailureMediumConfidence,
                    FailureSeverity.High => MemoryGovernanceDefaults.DefaultFailureHighConfidence,
                    FailureSeverity.Critical => MemoryGovernanceDefaults.DefaultFailureCriticalConfidence,
                    _ => MemoryGovernanceDefaults.DefaultFailureFallbackConfidence
                };
            else
                record.ConfidenceScore = MemoryConfidenceNormalizer.Normalize(record.ConfidenceScore.Value);
        }

        public static void Ensure(SuccessRecord record)
        {
            if (record.ConfidenceScore is null)
                record.ConfidenceScore = MemoryGovernanceDefaults.DefaultSuccessConfidence;
            else
                record.ConfidenceScore = MemoryConfidenceNormalizer.Normalize(record.ConfidenceScore.Value);
        }
    }
}
