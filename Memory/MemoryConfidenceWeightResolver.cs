namespace LocalCursorAgent.Memory
{
    internal static class MemoryConfidenceWeightResolver
    {
        public static double Resolve(double? confidenceScore)
        {
            if (!confidenceScore.HasValue)
                return 1.0;

            var value = confidenceScore.Value;
            if (value < 0) value = 0;
            if (value > 1) value = 1;
            return 0.5 + (value * 0.5);
        }
    }
}
