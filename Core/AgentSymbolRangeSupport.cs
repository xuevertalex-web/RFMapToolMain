using LocalCursorAgent.Indexing;
using System.Text.RegularExpressions;

namespace LocalCursorAgent.Core
{
    internal static class AgentSymbolRangeSupport
    {
        internal static bool IsMethodDeclarationKind(string declarationKind)
        {
            return declarationKind.Equals("method", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsClassDeclarationKind(string declarationKind)
        {
            return declarationKind.Equals("class", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool LooksLikeMethodDeclaration(string line)
        {
            return !string.IsNullOrWhiteSpace(line) &&
                   Regex.IsMatch(line, @"(?:public|private|protected|internal)(?:\s+static)?(?:\s+async)?\s+(?:virtual\s+)?(?:override\s+)?[\w<>\.\[\],]+\s+\w+\s*\(");
        }

        internal static bool LooksLikeClassDeclaration(string line)
        {
            return !string.IsNullOrWhiteSpace(line) &&
                   Regex.IsMatch(line, @"(?:public|private|protected|internal)?\s*(?:abstract|sealed|static)?\s*(?:partial\s+)?class\s+\w+");
        }

        internal static int ClampAnchorLineIndex(string[] lines, int anchorLineIndex)
        {
            return Math.Min(anchorLineIndex, lines.Length - 1);
        }

        internal static bool ShouldReturnNotFound(string[] lines, string needle)
        {
            return string.IsNullOrWhiteSpace(needle) || HasNoLines(lines);
        }

        internal static bool IsFoundLineIndex(int lineIndex)
        {
            return lineIndex >= 0;
        }

        internal static bool ContainsSymbolIgnoreCase(string line, string symbol)
        {
            return line.IndexOf(symbol, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static int FindFirstLineIndex(string[] lines, Func<string, bool> predicate)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (predicate(lines[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        internal static string NormalizeLineForDeclarationMatch(string line)
        {
            return line.Trim();
        }

        internal static bool HasNoSymbol(string symbol)
        {
            return string.IsNullOrWhiteSpace(symbol);
        }

        internal static bool IsBlockClosed(int braceDepth)
        {
            return braceDepth <= 0;
        }

        internal static char GetCharAt(string line, int index)
        {
            return line[index];
        }

        internal static int GetLineLength(string line)
        {
            return line.Length;
        }

        internal static bool HasNoLines(string[] lines)
        {
            return lines.Length == 0;
        }

        internal static int ToOneBasedLineNumber(int zeroBasedLineIndex)
        {
            return Math.Max(1, zeroBasedLineIndex + 1);
        }

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
