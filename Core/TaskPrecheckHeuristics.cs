using System.Text.RegularExpressions;

namespace LocalCursorAgent.Core
{
    internal static class TaskPrecheckHeuristics
    {
        public static bool IsAnalysisOnlyTask(string task)
        {
            if (string.IsNullOrWhiteSpace(task))
                return false;

            var normalized = task.ToLowerInvariant();
            return normalized.Contains("analyze") ||
                   normalized.Contains("analyse") ||
                   normalized.Contains("summarize") ||
                   normalized.Contains("summarise") ||
                   normalized.Contains("explain") ||
                   normalized.Contains("review") ||
                   normalized.Contains("diagnose") ||
                   normalized.Contains("опиши") ||
                   normalized.Contains("описать") ||
                   normalized.Contains("объясни") ||
                   normalized.Contains("объяснить") ||
                   normalized.Contains("обзор") ||
                   normalized.Contains("структуру") ||
                   normalized.Contains("структура") ||
                   normalized.Contains("ключевые файлы") ||
                   normalized.Contains("расскажи");
        }

        public static bool IsSuspiciousInjectedToolTask(string task)
        {
            if (string.IsNullOrWhiteSpace(task))
                return false;

            var normalized = task.Trim();
            return normalized.Contains("TOOL:", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("INPUT:", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsLowSignalTask(string task)
        {
            if (string.IsNullOrWhiteSpace(task))
                return true;

            var normalized = task.Trim();
            if (Path.IsPathRooted(normalized) && !normalized.Contains(' '))
                return true;

            var signalChars = normalized.Count(char.IsLetterOrDigit);
            if (signalChars < 3)
                return true;

            if (normalized.Length >= 256)
            {
                var signalRatio = (double)signalChars / normalized.Length;
                var substantiveTokenCount = Regex.Matches(normalized, @"[\p{L}\p{Nd}_]{3,}").Count;
                if (signalRatio < 0.35 || substantiveTokenCount < 3)
                    return true;
            }

            var intent = TaskIntentScorer.Classify(normalized);
            return intent == TaskIntentKind.Clarify && signalChars < 8;
        }
    }
}
