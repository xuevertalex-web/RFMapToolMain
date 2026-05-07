namespace LocalCursorAgent.Memory
{
    internal static class MemoryConfidenceWeightResolver
    {
        public static double Resolve(double? confidenceScore)
        {
            if (!confidenceScore.HasValue)
                return 1.0;

            var value = MemoryConfidenceNormalizer.Normalize(confidenceScore.Value);
            return 0.5 + (value * 0.5);
        }
    }
}
