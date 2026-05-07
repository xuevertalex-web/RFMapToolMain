namespace LocalCursorAgent.Memory
{
    internal static class MemoryConfidenceNormalizer
    {
        public static double Normalize(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }
    }
}
