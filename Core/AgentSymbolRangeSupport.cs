using LocalCursorAgent.Indexing;

namespace LocalCursorAgent.Core
{
    internal static class AgentSymbolRangeSupport
    {
        internal static string[]? TryReadAllLines(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            var lines = File.ReadAllLines(filePath);
            return lines.Length is 0 ? null : lines;
        }

        internal static List<string> GetIndexedSymbolsOrEmpty(ProjectSymbolDirectory? symbolDirectory, string filePath)
        {
            return symbolDirectory?.GetSymbols(filePath) ?? [];
        }

        internal static IEnumerable<string> DistinctIgnoreCase(IEnumerable<string> values)
        {
            return values.Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }
}
