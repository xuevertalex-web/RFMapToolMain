using System.Text.Json;
using LocalCursorAgent.Tools;

namespace LocalCursorAgent.Configuration
{
    /// <summary>
    /// Manages agent configuration and file exclusion rules.
    /// Loads configuration from workspace root.
    /// </summary>
    public class AgentConfig
    {
        private List<string> _excludePatterns = new();
        private readonly string _workspaceRoot;
        private readonly string _configPath;
        private readonly TextFileService _textFileService = new();
        private bool _disableEmbeddings;

        public List<string> ExcludePatterns => _excludePatterns;
        public string WorkspaceRoot => _workspaceRoot;
        public bool DisableEmbeddings => _disableEmbeddings;

        public AgentConfig(string workspaceRoot = "")
        {
            // Use current directory if workspace root not specified
            _workspaceRoot = string.IsNullOrEmpty(workspaceRoot) 
                ? Directory.GetCurrentDirectory() 
                : workspaceRoot;

            _configPath = Path.Combine(_workspaceRoot, "agent.config.json");
            LoadConfig();
        }

        /// <summary>
        /// Load configuration from agent.config.json in workspace root.
        /// </summary>
        public void LoadConfig()
        {
            if (!File.Exists(_configPath))
            {
                _excludePatterns = new List<string> 
                { 
                    "bin/", "obj/", ".git/", ".vs/", "node_modules/", "packages/", ".agent-runtime/"
                };
                _disableEmbeddings = false;
                return;
            }

            var json = _textFileService.ReadAsync(_configPath).GetAwaiter().GetResult().TextContent;
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            _excludePatterns = new List<string>();
            if (root.TryGetProperty("exclude", out var excludeElement))
            {
                foreach (var item in excludeElement.EnumerateArray())
                {
                    var pattern = item.GetString();
                    if (pattern != null)
                        _excludePatterns.Add(pattern);
                }
            }

            _disableEmbeddings = root.TryGetProperty("disableEmbeddings", out var disableEmbeddingsElement)
                && disableEmbeddingsElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                && disableEmbeddingsElement.GetBoolean();

            Console.WriteLine($"Loaded {_excludePatterns.Count} exclusion patterns from agent.config.json");
            if (_disableEmbeddings)
            {
                Console.WriteLine("Embeddings disabled by agent.config.json");
            }
        }

        /// <summary>
        /// Check if a file path should be excluded from indexing.
        /// Normalizes paths and checks against exclusion patterns.
        /// NEVER excludes .csproj files.
        /// </summary>
        public bool IsExcluded(string filePath)
        {
            // NEVER exclude .csproj files
            if (filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                return false;

            // Normalize path to use forward slashes and be relative to workspace root
            var relativePath = GetRelativePath(filePath);
            var normalizedPath = relativePath.Replace("\\", "/");

            // Check each exclusion pattern
            foreach (var pattern in _excludePatterns)
            {
                if (MatchesPattern(normalizedPath, pattern))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Get path relative to workspace root.
        /// </summary>
        private string GetRelativePath(string filePath)
        {
            if (Path.IsPathFullyQualified(filePath) && filePath.StartsWith(_workspaceRoot))
            {
                return Path.GetRelativePath(_workspaceRoot, filePath);
            }
            return filePath;
        }

        /// <summary>
        /// Check if a normalized path matches an exclusion pattern.
        /// Patterns like "bin/", "obj/" match directories.
        /// </summary>
        private bool MatchesPattern(string normalizedPath, string pattern)
        {
            // Pattern with trailing slash = directory exclusion
            if (pattern.EndsWith("/"))
            {
                var dir = pattern.TrimEnd('/');
                // Match: "bin/xyz", "bin/xyz/file.cs", or starts with "bin/"
                return normalizedPath == dir || 
                       normalizedPath.StartsWith($"{dir}/");
            }

            // Exact match
            return normalizedPath == pattern;
        }

        /// <summary>
        /// Get all .cs files in workspace, excluding configured directories.
        /// </summary>
        public List<string> GetSourceFiles()
        {
            return Directory.GetFiles(_workspaceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(f => !IsExcluded(f))
                .ToList();
        }
    }
}
