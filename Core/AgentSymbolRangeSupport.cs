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

        internal static int FindMatchingLine(string[] lines, string needle)
        {
            if (ShouldReturnNotFound(lines, needle))
            {
                return -1;
            }

            const StringComparison NeedleComparison = StringComparison.OrdinalIgnoreCase;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(needle, NeedleComparison) >= 0)
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

        internal static List<string> NormalizeIndexedSymbols(List<string> indexedSymbols)
        {
            return indexedSymbols
                .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static List<string> BuildSearchOrder(List<string> indexedSymbols, string candidate)
        {
            var searchOrder = NormalizeIndexedSymbols(indexedSymbols);
            if (!searchOrder.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                searchOrder.Insert(0, candidate);
            }

            return searchOrder;
        }

        internal static string GetFallbackCandidateName(string filePath)
        {
            return Path.GetFileNameWithoutExtension(filePath);
        }

        internal static bool IsDeclarationLineContainingSymbol(string line, string symbol, StringComparison comparison)
        {
            return line.Contains(symbol, comparison) &&
                   (LooksLikeMethodDeclaration(line) || LooksLikeClassDeclaration(line));
        }

        internal static int FindMatchingBlockEnd(string[] lines, int startLineIndex)
        {
            var braceDepth = 0;
            var seenOpeningBrace = false;
            const int openingBraceDelta = 1;
            const int closingBraceDelta = -1;

            for (var i = startLineIndex; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineLength = GetLineLength(line);
                for (var j = 0; j < lineLength; j++)
                {
                    switch (GetCharAt(line, j))
                    {
                        case '{':
                            braceDepth += openingBraceDelta;
                            seenOpeningBrace = true;
                            break;
                        case '}':
                            braceDepth += closingBraceDelta;
                            if (seenOpeningBrace && IsBlockClosed(braceDepth))
                                return i;
                            break;
                    }
                }
            }

            return startLineIndex;
        }

        internal static (int startLine, int endLine) BuildBlockRangeFromDeclaration(string[] lines, int declarationStart)
        {
            var blockEnd = FindMatchingBlockEnd(lines, declarationStart);
            var startLine = declarationStart + 1;
            var endLine = Math.Max(startLine, blockEnd + 1);
            return (startLine, endLine);
        }

        internal static int FindNearestDeclarationStart(string[] lines, int anchorLineIndex, string declarationKind)
        {
            if (HasNoLines(lines))
                return -1;

            var isMethod = IsMethodDeclarationKind(declarationKind);
            var isClass = IsClassDeclarationKind(declarationKind);
            var startIndex = ClampAnchorLineIndex(lines, anchorLineIndex);
            if (!isMethod && !isClass)
                return -1;

            for (var i = startIndex; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (isMethod)
                {
                    if (LooksLikeMethodDeclaration(line))
                    {
                        return i;
                    }
                }

                if (isClass && LooksLikeClassDeclaration(line))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
