namespace LocalCursorAgent.Memory
{
    internal static class MemoryConfidenceDecay
    {
        public static double Apply(double confidenceScore, double decayRate)
        {
            var normalizedScore = MemoryConfidenceNormalizer.Normalize(confidenceScore);
            if (decayRate <= 0)
                return normalizedScore;

            var decayed = normalizedScore * (1 - decayRate);
            return MemoryConfidenceNormalizer.Normalize(decayed);
        }
    }
}
