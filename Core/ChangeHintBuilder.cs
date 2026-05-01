using System.Text.RegularExpressions;

namespace LocalCursorAgent.Core
{
    internal static class ChangeHintBuilder
    {
        public static string ExtractActionHint(string toolInput, string fallbackSymbol)
        {
            var lower = toolInput.ToLowerInvariant();
            var symbol = ExtractTargetSymbol(toolInput) ?? fallbackSymbol;

            if (lower.Contains("validation"))
                return $"Added validation in {symbol}";
            if (lower.Contains("null"))
                return $"Added null check in {symbol}";
            if (lower.Contains("workspace") && lower.Contains("path"))
                return "Adjusted workspace path resolution";
            if (lower.Contains("build") && lower.Contains("error"))
                return $"Updated build error handling in {symbol}";
            if (lower.Contains("error handling"))
                return $"Updated error handling in {symbol}";
            if (lower.Contains("fix"))
                return $"Fixed {symbol}";
            if (lower.Contains("refactor"))
                return $"Refined {symbol}";

            return string.Empty;
        }

        public static string? ExtractTargetSymbol(string text)
        {
            var patterns = new[]
            {
                @"\bmethod\s+([A-Za-z_][A-Za-z0-9_]*)\b",
                @"\bclass\s+([A-Za-z_][A-Za-z0-9_]*)\b",
                @"\bfunction\s+([A-Za-z_][A-Za-z0-9_]*)\b",
                @"\bservice\s+([A-Za-z_][A-Za-z0-9_]*)\b",
                @"\b([A-Za-z_][A-Za-z0-9_]*)\s+method\b"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        public static string NormalizeHint(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var cleaned = Regex.Replace(text, @"\s+", " ").Trim();
            var firstSentence = cleaned.Split(new[] { '.', '!', '?' }, 2, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            if (firstSentence.Length > 120)
                firstSentence = firstSentence.Substring(0, 117).TrimEnd() + "...";
            return firstSentence;
        }
    }
}
