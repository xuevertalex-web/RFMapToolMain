using System.Text.RegularExpressions;

namespace LocalCursorAgent.Core;

internal static class TargetTokenHeuristics
{
    private static readonly HashSet<string> GenericSuffixTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "Service",
        "Controller",
        "Manager",
        "Helper",
        "Repository",
        "Handler"
    };

    public static bool IsMeaningfulPartialToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 4)
            return false;

        if (GenericSuffixTokens.Contains(token))
            return false;

        return Regex.IsMatch(token, @"[A-Za-z]{4,}");
    }

    public static bool IsMeaningfulPrefixMatch(string candidate, string token)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(token))
            return false;

        if (GenericSuffixTokens.Contains(token) && candidate.Equals(token, StringComparison.OrdinalIgnoreCase))
            return false;

        if (candidate.Equals(token, StringComparison.OrdinalIgnoreCase))
            return true;

        return candidate.StartsWith(token, StringComparison.OrdinalIgnoreCase) ||
               token.StartsWith(candidate, StringComparison.OrdinalIgnoreCase);
    }

    public static string ExtractTargetToken(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        var pathLike = Regex.Match(query, @"([A-Za-z0-9_\-./\\]+\.cs)\b");
        if (pathLike.Success)
        {
            var token = pathLike.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(token))
                return token;
        }

        var pathSegment = Regex.Match(query, @"(?:^|[\s\""'`])([A-Za-z0-9_\-]+(?:[\\/][A-Za-z0-9_\-./\\]+)+)(?:$|[\s\""'`,:;])");
        if (pathSegment.Success)
        {
            var token = pathSegment.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(token))
                return token;
        }

        var symbols = Regex.Matches(query, @"\b[A-Z][A-Za-z0-9_]{3,}\b")
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var suffixPreferred = symbols.FirstOrDefault(s => s.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
                                                          s.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
                                                          s.EndsWith("Manager", StringComparison.OrdinalIgnoreCase) ||
                                                          s.EndsWith("Handler", StringComparison.OrdinalIgnoreCase) ||
                                                          s.EndsWith("Repository", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(suffixPreferred))
            return suffixPreferred;

        return symbols.FirstOrDefault() ?? string.Empty;
    }

    public static string ClassifyToken(string query, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "Unknown";

        if (query.Contains('/') || query.Contains('\\') || token.Contains('/') || token.Contains('\\') || token.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return "FilenameLike";

        if (IsSymbolLikeToken(token))
            return "SymbolLike";

        return "Unknown";
    }

    public static bool IsSymbolLikeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        return token.Length >= 4 &&
               (token.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith("Manager", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith("Handler", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith("Repository", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith("Helper", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(token, @"^[A-Z][A-Za-z0-9_]+$"));
    }
}
