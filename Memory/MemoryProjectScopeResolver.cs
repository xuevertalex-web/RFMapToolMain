namespace LocalCursorAgent.Memory
{
    public static class MemoryProjectScopeResolver
    {
        private const string DefaultScope = MemoryGovernanceDefaults.DefaultProjectScope;
        private static readonly string[] ScopeMarkers = new[] { "scope:", "project:", "workspace:" };

        public static string Resolve(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return DefaultScope;

            var trimmed = query.Trim();
            foreach (var marker in ScopeMarkers)
            {
                var markerIndex = trimmed.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0)
                    continue;

                var start = markerIndex + marker.Length;
                while (start < trimmed.Length && char.IsWhiteSpace(trimmed[start]))
                    start++;

                if (start >= trimmed.Length)
                    continue;

                var end = start;
                while (end < trimmed.Length)
                {
                    var c = trimmed[end];
                    if (char.IsWhiteSpace(c) || c == ',' || c == ';' || c == ']' || c == ')')
                        break;
                    end++;
                }

                if (end <= start)
                    continue;

                var candidate = trimmed.Substring(start, end - start).Trim('\'', '"', '[', '(', ')');
                var normalized = NormalizeScope(candidate);
                if (!string.Equals(normalized, DefaultScope, System.StringComparison.Ordinal))
                    return normalized;
            }

            return DefaultScope;
        }

        public static string NormalizeScope(string? projectScope)
        {
            return string.IsNullOrWhiteSpace(projectScope) ? DefaultScope : projectScope.Trim();
        }

        public static bool IsSameScope(string? left, string? right)
        {
            return string.Equals(NormalizeScope(left), NormalizeScope(right), System.StringComparison.Ordinal);
        }
    }
}
