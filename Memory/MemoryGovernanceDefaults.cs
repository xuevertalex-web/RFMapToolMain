namespace LocalCursorAgent.Memory
{
    public static class MemoryGovernanceDefaults
    {
        public const string DefaultMemorySource = "local-agent";
        public const string DefaultProjectScope = "default";
        public const double ProfileDecayRate = 0.01;
        public const int MaxFailureRecords = 500;
        public const int MaxSuccessRecords = 500;
        public const double DefaultSuccessConfidence = 0.7;
        public const double DefaultFailureLowConfidence = 0.25;
        public const double DefaultFailureMediumConfidence = 0.5;
        public const double DefaultFailureHighConfidence = 0.75;
        public const double DefaultFailureCriticalConfidence = 0.9;
        public const double DefaultFailureFallbackConfidence = 0.4;
        public const double RelevanceScoreThreshold = 0.1;
        public const double ConfidenceRecalibrationSuccessDelta = 0.05;
        public const double ConfidenceRecalibrationFailureDelta = 0.07;
    }
}
