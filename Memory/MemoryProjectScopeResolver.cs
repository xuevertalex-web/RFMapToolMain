namespace LocalCursorAgent.Memory
{
    internal static class MemoryProjectScopeResolver
    {
        private const string DefaultScope = "default";

        public static string Resolve(string query)
        {
            return string.IsNullOrWhiteSpace(query) ? DefaultScope : DefaultScope;
        }
    }
}
