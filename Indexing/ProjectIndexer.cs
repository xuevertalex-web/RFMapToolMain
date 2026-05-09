using LocalCursorAgent.Embeddings;
using LocalCursorAgent.Configuration;
using LocalCursorAgent.Context;
using LocalCursorAgent.Tools;

namespace LocalCursorAgent.Indexing
{
    /// <summary>
    /// Indexes C# files and generates embeddings for semantic search.
    /// Includes symbol extraction for structural code awareness.
    /// Implements caching, deduplication, and graceful error handling.
    /// Integrates with file state manager for active context layer.
    /// </summary>
    public class ProjectIndexer
    {
        private readonly EmbeddingService _embeddingService;
        private readonly VectorStore _vectorStore;
        private readonly string _projectPath;
        private readonly AgentConfig _agentConfig;
        private readonly FileStateManager? _fileStateManager;
        private readonly ProjectSymbolDirectory _symbolDirectory;
        private readonly TextFileService _textFileService;
        private List<string>? _cachedFileList;
        private readonly Dictionary<string, FileIndexCacheEntry> _fileIndexCache = new(StringComparer.OrdinalIgnoreCase);
        private int _cacheHits;
        private int _cacheMisses;

        public ProjectIndexer(string projectPath, EmbeddingService embeddingService, VectorStore vectorStore, AgentConfig? agentConfig = null, FileStateManager? fileStateManager = null)
        {
            _projectPath = projectPath;
            _embeddingService = embeddingService;
            _vectorStore = vectorStore;
            _agentConfig = agentConfig ?? new AgentConfig(projectPath);
            _fileStateManager = fileStateManager;
            _symbolDirectory = new ProjectSymbolDirectory();
            _textFileService = new TextFileService();
        }

        /// <summary>
        /// Get all valid source files (cached per session).
        /// Deterministic order for reproducibility.
        /// </summary>
        private List<string> GetSourceFiles()
        {
            if (_cachedFileList != null)
                return _cachedFileList;

            if (!Directory.Exists(_projectPath))
                return new List<string>();

            _cachedFileList = Directory.GetFiles(_projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !_agentConfig.IsExcluded(f))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return _cachedFileList;
        }

        /// <summary>
        /// Index all C# files in the project.
        /// Gracefully handles errors and prevents duplicate indexing.
        /// Initializes file state as Clean after indexing.
        /// </summary>
        public async Task<IndexResult> IndexProject()
        {
            var result = new IndexResult();
            _cacheHits = 0;
            _cacheMisses = 0;

            if (!Directory.Exists(_projectPath))
            {
                result.Success = false;
                result.Error = $"Project path not found: {_projectPath}";
                return result;
            }

            var csFiles = GetSourceFiles();
            Console.WriteLine($"Found {csFiles.Count} C# files");

            foreach (var filePath in csFiles)
            {
                var relativePath = Path.GetRelativePath(_projectPath, filePath);

                var embeddingsDisabled = _embeddingService.Status == EmbeddingRuntimeStatus.Disabled;
                if (TryUseCache(relativePath, filePath) && (_vectorStore.IsIndexed(relativePath) || embeddingsDisabled))
                {
                    result.FilesProcessed++;
                    result.IndexedFiles.Add(relativePath);
                    _fileStateManager?.MarkClean(relativePath);
                    continue;
                }

                if (!await IndexSingleFile(filePath, relativePath, result))
                {
                    result.Errors.Add($"Failed to index {relativePath}");
                }
            }

            result.Success = true;

            if (_fileStateManager != null && result.IndexedFiles.Count > 0)
            {
                _fileStateManager.InitializeFilesAsClean(result.IndexedFiles);
            }

            return result;
        }

        /// <summary>
        /// Index a single file with error handling.
        /// Extracts symbols and stores embeddings when available.
        /// </summary>
        private async Task<bool> IndexSingleFile(string fullPath, string relativePath, IndexResult result)
        {
            try
            {
                var snapshot = await _textFileService.ReadAsync(fullPath);
                var content = snapshot.NormalizedText;

                var symbols = SymbolIndexer.ExtractSymbols(content);
                _symbolDirectory.RegisterFile(relativePath, symbols);
                UpdateCache(relativePath, fullPath, symbols);

                if (symbols.Count > 0)
                {
                    Console.WriteLine($"  Extracted {symbols.Count} symbols from {Path.GetFileName(relativePath)}");
                }

                var embedding = await _embeddingService.GenerateEmbedding(content);
                if (embedding != null)
                {
                    _vectorStore.AddVector(relativePath, embedding, content);
                }

                result.IndexedFiles.Add(relativePath);
                result.FilesProcessed++;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[indexing] Error indexing {relativePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Find relevant files based on semantic search.
        /// Falls back to filename matching if embeddings are unavailable.
        /// </summary>
        public async Task<List<string>> FindRelevantFiles(string query, int topK = 10)
        {
            topK = Math.Max(5, Math.Min(topK, 15));

            var embedding = await _embeddingService.GenerateEmbedding(query);
            if (embedding == null)
                return FindRelevantFilesWithoutEmbeddings(query, topK);

            var similarFiles = _vectorStore.FindSimilar(embedding, topK);
            return similarFiles.Select(x => x.identifier).Distinct().ToList();
        }

        /// <summary>
        /// Re-index a single file with error handling.
        /// Marks file as Clean after the file has been processed, even if embeddings are degraded.
        /// </summary>
        public async Task<bool> ReindexFile(string relativePath)
        {
            try
            {
                var fullPath = Path.Combine(_projectPath, relativePath);
                if (!File.Exists(fullPath))
                    return false;

                var snapshot = await _textFileService.ReadAsync(fullPath);
                var content = snapshot.NormalizedText;

                var symbols = SymbolIndexer.ExtractSymbols(content);
                _symbolDirectory.RegisterFile(relativePath, symbols);
                UpdateCache(relativePath, fullPath, symbols);

                var embedding = await _embeddingService.GenerateEmbedding(content);
                if (embedding != null)
                {
                    _vectorStore.AddVector(relativePath, embedding, content);
                }

                _fileStateManager?.MarkClean(relativePath);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[indexing] Error re-indexing {relativePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear session cache to force re-scan on next indexing.
        /// </summary>
        public void ClearCache()
        {
            _cachedFileList = null;
            _fileIndexCache.Clear();
            _cacheHits = 0;
            _cacheMisses = 0;
        }

        /// <summary>
        /// Get the symbol directory for context-aware ranking.
        /// </summary>
        public ProjectSymbolDirectory SymbolDirectory => _symbolDirectory;

        /// <summary>
        /// Get all indexed file identifiers in deterministic order.
        /// </summary>
        public IReadOnlyList<string> GetIndexedFiles()
        {
            return _vectorStore.GetAllIdentifiers().ToList();
        }

        public int CacheHits => _cacheHits;
        public int CacheMisses => _cacheMisses;

        private List<string> FindRelevantFilesWithoutEmbeddings(string query, int topK)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<string>();

            var queryTerms = query
                .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ':', ';', '(', ')', '[', ']', '{', '}', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(term => term.Length >= 2)
                .Select(term => term.ToLowerInvariant())
                .Distinct()
                .ToList();

            var ranked = GetSourceFiles()
                .Select(path => Path.GetRelativePath(_projectPath, path))
                .Select(relativePath => new
                {
                    RelativePath = relativePath,
                    Score = queryTerms.Count(term => relativePath.Contains(term, StringComparison.OrdinalIgnoreCase))
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Take(topK)
                .Select(item => item.RelativePath)
                .ToList();

            if (ranked.Count > 0)
                return ranked;

            return GetSourceFiles()
                .Select(path => Path.GetRelativePath(_projectPath, path))
                .Take(topK)
                .ToList();
        }

        private bool TryUseCache(string relativePath, string fullPath)
        {
            if (!_fileIndexCache.TryGetValue(relativePath, out var cacheEntry))
            {
                _cacheMisses++;
                return false;
            }

            var mtimeUtc = File.GetLastWriteTimeUtc(fullPath);
            if (cacheEntry.LastWriteTimeUtc != mtimeUtc)
            {
                _cacheMisses++;
                return false;
            }

            _symbolDirectory.RegisterFile(relativePath, SymbolIndexer.CloneSymbols(cacheEntry.Symbols));
            _cacheHits++;
            return true;
        }

        private void UpdateCache(string relativePath, string fullPath, List<string> symbols)
        {
            _fileIndexCache[relativePath] = new FileIndexCacheEntry
            {
                RelativePath = relativePath,
                LastWriteTimeUtc = File.GetLastWriteTimeUtc(fullPath),
                Symbols = SymbolIndexer.CloneSymbols(symbols)
            };
        }

        private sealed class FileIndexCacheEntry
        {
            public string RelativePath { get; set; } = string.Empty;
            public DateTime LastWriteTimeUtc { get; set; }
            public List<string> Symbols { get; set; } = new();
        }

        /// <summary>
        /// Result of indexing operation.
        /// </summary>
        public class IndexResult
        {
            public bool Success { get; set; }
            public int FilesProcessed { get; set; }
            public List<string> IndexedFiles { get; set; } = new();
            public List<string> Errors { get; set; } = new();
            public string Error { get; set; } = string.Empty;
        }
    }
}
