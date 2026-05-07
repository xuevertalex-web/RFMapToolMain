namespace LocalCursorAgent.Memory
{
    public static class MemoryConfidenceRecalibrator
    {
        public static double Recalibrate(double confidence, bool success)
        {
            var normalized = MemoryConfidenceNormalizer.Normalize(confidence);
            var delta = success
                ? MemoryGovernanceDefaults.ConfidenceRecalibrationSuccessDelta
                : -MemoryGovernanceDefaults.ConfidenceRecalibrationFailureDelta;
            return MemoryConfidenceNormalizer.Normalize(normalized + delta);
        }
    }
}
