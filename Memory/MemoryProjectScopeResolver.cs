namespace LocalCursorAgent.Memory
{
    internal static class MemoryProjectScopeResolver
    {
        private const string DefaultScope = MemoryGovernanceDefaults.DefaultProjectScope;

        public static string Resolve(string query)
        {
            return string.IsNullOrWhiteSpace(query) ? DefaultScope : DefaultScope;
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
