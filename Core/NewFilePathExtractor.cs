using System.Text.RegularExpressions;

namespace LocalCursorAgent.Core
{
    internal static class NewFilePathExtractor
    {
        public static string? ExtractRequestedNewFilePath(string task)
        {
            if (string.IsNullOrWhiteSpace(task))
                return null;

            var normalized = task.ToLowerInvariant();
            var isCreateIntent = normalized.Contains("create") ||
                                 normalized.Contains("generate") ||
                                 normalized.Contains("write") ||
                                 normalized.Contains("make") ||
                                 normalized.Contains("создай") ||
                                 normalized.Contains("создать") ||
                                 normalized.Contains("добавь") ||
                                 normalized.Contains("добавить") ||
                                 normalized.Contains("напиши") ||
                                 normalized.Contains("сделай") ||
                                 normalized.Contains("саздай") ||
                                 normalized.Contains("зделай") ||
                                 normalized.Contains("напеши");

            if (!isCreateIntent)
                return null;

            var fileMatch = Regex.Match(task, @"([A-Za-z0-9_\-./\\]+\.[A-Za-z0-9_\-]+)\b");
            if (!fileMatch.Success)
                return null;

            var candidate = fileMatch.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
                return null;

            if (IsUrlLikeToken(task, fileMatch.Index, fileMatch.Length, candidate))
                return null;

            return candidate;
        }

        private static bool IsUrlLikeToken(string task, int matchIndex, int matchLength, string candidate)
        {
            if (Regex.IsMatch(candidate, @"^[a-zA-Z][a-zA-Z0-9+\-.]*://"))
                return true;

            var tokenStart = matchIndex;
            while (tokenStart > 0)
            {
                var ch = task[tokenStart - 1];
                if (char.IsWhiteSpace(ch) || ch == '"' || ch == '\'' || ch == '(' || ch == '[' || ch == '{' || ch == '<')
                    break;
                tokenStart--;
            }

            var tokenEnd = matchIndex + matchLength;
            while (tokenEnd < task.Length)
            {
                var ch = task[tokenEnd];
                if (char.IsWhiteSpace(ch) || ch == '"' || ch == '\'' || ch == ')' || ch == ']' || ch == '}' || ch == '>')
                    break;
                tokenEnd++;
            }

            var surroundingToken = task[tokenStart..tokenEnd];
            return surroundingToken.Contains("://", StringComparison.Ordinal);
        }
    }
}
