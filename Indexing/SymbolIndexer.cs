using System.Text.RegularExpressions;

namespace LocalCursorAgent.Indexing
{
    /// <summary>
    /// Lightweight C# symbol extraction using regex patterns.
    /// No Roslyn, no AST parsing - simple and fast.
    /// </summary>
    public class SymbolIndexer
    {
        /// <summary>
        /// Extract symbols from C# file content.
        /// Returns: class names, interface names, method names, property names.
        /// </summary>
        public static List<string> ExtractSymbols(string fileContent)
        {
            var symbols = new List<string>();

            // Extract class names
            var classMatches = Regex.Matches(fileContent, @"(?:public|private|protected|internal)?\s*(?:abstract|sealed)?\s*class\s+(\w+)");
            foreach (Match match in classMatches)
            {
                var symbol = match.Groups[1].Value;
                if (!string.IsNullOrEmpty(symbol))
                    symbols.Add(symbol);
            }

            // Extract interface names
            var interfaceMatches = Regex.Matches(fileContent, @"(?:public|private|protected|internal)?\s*interface\s+(\w+)");
            foreach (Match match in interfaceMatches)
            {
                var symbol = match.Groups[1].Value;
                if (!string.IsNullOrEmpty(symbol))
                    symbols.Add(symbol);
            }

            // Extract method names (public/protected/private/internal methods)
            // Pattern: modifier(s) + return_type + method_name + (
            // More flexible to catch static, async, virtual, override methods
            var methodMatches = Regex.Matches(fileContent, @"(?:public|private|protected|internal)(?:\s+static)?(?:\s+async)?\s+(?:virtual\s+)?(?:override\s+)?[\w<>\.]+\s+(\w+)\s*\(");
            foreach (Match match in methodMatches)
            {
                var symbol = match.Groups[1].Value;
                // Filter out constructors and common keywords
                if (!string.IsNullOrEmpty(symbol) && symbol != "if" && symbol != "for" && symbol != "while" && symbol != "using" && symbol != "switch")
                    symbols.Add(symbol);
            }

            // Extract property names (public/protected properties)
            // Pattern: modifier(s) + type + property_name + { ... } or =
            var propertyMatches = Regex.Matches(fileContent, @"(?:public|protected)\s+(?:virtual\s+)?(?:override\s+)?[\w<>\.]+\s+(\w+)\s*(?:{|})")
            ;
            foreach (Match match in propertyMatches)
            {
                var symbol = match.Groups[1].Value;
                if (!string.IsNullOrEmpty(symbol))
                    symbols.Add(symbol);
            }

            // Deduplicate and sort for determinism
            return symbols
                .Distinct(StringComparer.Ordinal)
                .OrderBy(s => s)
                .ToList();
        }

        /// <summary>
        /// Check if query contains any symbol from file.
        /// Case-sensitive to avoid false positives.
        /// </summary>
        public static bool QueryContainsSymbol(string query, List<string> fileSymbols)
        {
            if (fileSymbols.Count == 0)
                return false;

            foreach (var symbol in fileSymbols)
            {
                if (query.Contains(symbol, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Count how many symbols from file match query.
        /// Used for precise ranking when multiple symbols match.
        /// </summary>
        public static int CountMatchingSymbols(string query, List<string> fileSymbols)
        {
            if (fileSymbols.Count == 0)
                return 0;

            return fileSymbols.Count(symbol => query.Contains(symbol, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Per-file symbol index mapping.
    /// </summary>
    public class FileSymbolIndex
    {
        public string FilePath { get; set; }
        public List<string> Symbols { get; set; } = new();

        public FileSymbolIndex(string filePath)
        {
            FilePath = filePath;
        }

        public override string ToString()
        {
            return $"{FilePath}: {Symbols.Count} symbols";
        }
    }

    /// <summary>
    /// Project-wide symbol directory.
    /// Maps file paths to their indexed symbols.
    /// </summary>
    public class ProjectSymbolDirectory
    {
        private readonly Dictionary<string, List<string>> _symbolsByFile = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Register symbols for a file.
        /// </summary>
        public void RegisterFile(string filePath, List<string> symbols)
        {
            _symbolsByFile[filePath] = symbols;
        }

        /// <summary>
        /// Get symbols for a specific file.
        /// </summary>
        public List<string> GetSymbols(string filePath)
        {
            if (_symbolsByFile.TryGetValue(filePath, out var symbols))
                return symbols;

            return new List<string>();
        }

        /// <summary>
        /// Find files containing a specific symbol.
        /// Useful for precise context selection.
        /// </summary>
        public List<string> FindFilesWithSymbol(string symbol)
        {
            var result = new List<string>();

            foreach (var (filePath, symbols) in _symbolsByFile)
            {
                if (symbols.Contains(symbol, StringComparer.Ordinal))
                    result.Add(filePath);
            }

            return result;
        }

        /// <summary>
        /// Clear all registered symbols.
        /// </summary>
        public void Clear()
        {
            _symbolsByFile.Clear();
        }

        /// <summary>
        /// Get total symbol count across all files.
        /// </summary>
        public int TotalSymbols => _symbolsByFile.Values.Sum(s => s.Count);

        /// <summary>
        /// Get count of indexed files.
        /// </summary>
        public int IndexedFileCount => _symbolsByFile.Count;

        /// <summary>
        /// Enumerate indexed file paths in deterministic order.
        /// </summary>
        public IEnumerable<string> AllFiles => _symbolsByFile.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }
}
