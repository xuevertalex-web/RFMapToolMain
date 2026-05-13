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
                                 normalized.Contains("\u0441\u043e\u0437\u0434\u0430\u0439") ||
                                 normalized.Contains("\u0441\u043e\u0437\u0434\u0430\u0442\u044c") ||
                                 normalized.Contains("\u0434\u043e\u0431\u0430\u0432\u044c") ||
                                 normalized.Contains("\u0434\u043e\u0431\u0430\u0432\u0438\u0442\u044c") ||
                                 normalized.Contains("\u043d\u0430\u043f\u0438\u0448\u0438") ||
                                 normalized.Contains("\u043d\u0430\u043f\u0435\u0448\u0438") ||
                                 normalized.Contains("\u0441\u0434\u0435\u043b\u0430\u0439") ||
                                 normalized.Contains("\u0441\u0434\u0435\u043b\u0430\u0442\u044c") ||
                                 normalized.Contains("\u0441\u0430\u0437\u0434\u0430\u0439") ||
                                 normalized.Contains("\u0437\u0434\u0435\u043b\u0430\u0439") ||
                                 normalized.Contains("\u0441\u0433\u0435\u043d\u0435\u0440\u0438\u0440\u0443\u0439");

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
