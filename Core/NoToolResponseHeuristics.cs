namespace LocalCursorAgent.Core
{
    internal static class NoToolResponseHeuristics
    {
        public static bool IsNonSubstantiveNoToolResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return true;

            var normalized = response.Trim();
            if (normalized.Length < 32)
                return true;

            var lower = normalized.ToLowerInvariant();
            return lower.StartsWith("i will analyze", StringComparison.Ordinal) ||
                   lower.StartsWith("i will review", StringComparison.Ordinal) ||
                   lower.StartsWith("i will examine", StringComparison.Ordinal) ||
                   lower.StartsWith("i will inspect", StringComparison.Ordinal) ||
                   lower.StartsWith("i will look for", StringComparison.Ordinal) ||
                   lower.StartsWith("i'll analyze", StringComparison.Ordinal) ||
                   lower.StartsWith("i'll review", StringComparison.Ordinal) ||
                   lower.StartsWith("i'll examine", StringComparison.Ordinal) ||
                   lower.StartsWith("i'll inspect", StringComparison.Ordinal) ||
                   lower.StartsWith("\u044f \u043f\u0440\u043e\u0430\u043d\u0430\u043b\u0438\u0437\u0438\u0440\u0443\u044e", StringComparison.Ordinal) ||
                   lower.StartsWith("\u044f \u043f\u0440\u043e\u0432\u0435\u0440\u044e", StringComparison.Ordinal) ||
                   lower.StartsWith("\u044f \u043d\u0430\u0439\u0434\u0443", StringComparison.Ordinal) ||
                   lower.StartsWith("\u044f \u0440\u0430\u0441\u0441\u043c\u043e\u0442\u0440\u044e", StringComparison.Ordinal);
        }

        public static bool IsNeedsMoreDataResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return false;

            var lower = response.Trim().ToLowerInvariant();
            return lower.Contains("need more information", StringComparison.Ordinal) ||
                   lower.Contains("provide more information", StringComparison.Ordinal) ||
                   lower.Contains("provide code", StringComparison.Ordinal) ||
                   lower.Contains("provide the code", StringComparison.Ordinal) ||
                   lower.Contains("need the code", StringComparison.Ordinal) ||
                   lower.Contains("нужно больше информации", StringComparison.Ordinal) ||
                   lower.Contains("предоставьте больше информации", StringComparison.Ordinal) ||
                   lower.Contains("предоставьте код", StringComparison.Ordinal) ||
                   lower.Contains("нужен код", StringComparison.Ordinal);
        }
    }
}
