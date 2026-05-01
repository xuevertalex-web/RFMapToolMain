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

            if (Regex.IsMatch(candidate, @"^[a-zA-Z][a-zA-Z0-9+\-.]*://"))
                return null;

            var tokenStart = fileMatch.Index;
            while (tokenStart > 0)
            {
                var ch = task[tokenStart - 1];
                if (char.IsWhiteSpace(ch) || ch == '"' || ch == '\'' || ch == '(' || ch == '[' || ch == '{' || ch == '<')
                    break;
                tokenStart--;
            }

            var tokenEnd = fileMatch.Index + fileMatch.Length;
            while (tokenEnd < task.Length)
            {
                var ch = task[tokenEnd];
                if (char.IsWhiteSpace(ch) || ch == '"' || ch == '\'' || ch == ')' || ch == ']' || ch == '}' || ch == '>')
                    break;
                tokenEnd++;
            }

            var surroundingToken = task[tokenStart..tokenEnd];
            if (surroundingToken.Contains("://", StringComparison.Ordinal))
                return null;

            return candidate;
        }
    }
}
