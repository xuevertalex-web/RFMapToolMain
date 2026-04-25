namespace LocalCursorAgent.Context
{
    /// <summary>
    /// File state enum for active context layer.
    /// Tracks which files are currently important or unstable.
    /// </summary>
    public enum FileState
    {
        /// <summary>
        /// File is stable and up-to-date in embeddings.
        /// </summary>
        Clean = 0,

        /// <summary>
        /// File was modified but not yet re-embedded.
        /// Marked as unstable context source.
        /// </summary>
        Dirty = 1,

        /// <summary>
        /// File was recently patched or used in current iteration.
        /// Highest priority in context ranking.
        /// </summary>
        Hot = 2
    }

    /// <summary>
    /// Lightweight session-based file state tracking.
    /// Enables awareness of "what is currently important in the workspace".
    /// </summary>
    public class FileStateManager
    {
        private readonly Dictionary<string, FileState> _fileStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastModifiedTime = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastPatchedTime = new(StringComparer.OrdinalIgnoreCase);
        private readonly int _hotFileThresholdMinutes;

        /// <summary>
        /// Create a new file state manager.
        /// </summary>
        /// <param name="hotFileThresholdMinutes">Minutes to consider a file "Hot" after modification.</param>
        public FileStateManager(int hotFileThresholdMinutes = 10)
        {
            _hotFileThresholdMinutes = hotFileThresholdMinutes;
        }

        /// <summary>
        /// Mark a file as having been modified on disk.
        /// </summary>
        public void MarkDirty(string filePath)
        {
            _fileStates[filePath] = FileState.Dirty;
            _lastModifiedTime[filePath] = DateTime.UtcNow;
        }

        /// <summary>
        /// Mark a file as having been recently patched.
        /// This is the highest priority state - file is both Dirty and Hot.
        /// </summary>
        public void MarkHot(string filePath)
        {
            _fileStates[filePath] = FileState.Hot;
            _lastPatchedTime[filePath] = DateTime.UtcNow;
            _lastModifiedTime[filePath] = DateTime.UtcNow;
        }

        /// <summary>
        /// Mark a file as Clean (stable and up-to-date in embeddings).
        /// Called after successful embedding update or initial indexing.
        /// </summary>
        public void MarkClean(string filePath)
        {
            _fileStates[filePath] = FileState.Clean;
        }

        /// <summary>
        /// Get the current state of a file.
        /// Defaults to Clean if file has not been explicitly marked.
        /// </summary>
        public FileState GetState(string filePath)
        {
            if (_fileStates.TryGetValue(filePath, out var state))
            {
                // If file is Hot, check if it has aged past the threshold
                if (state == FileState.Hot && _lastPatchedTime.TryGetValue(filePath, out var patchTime))
                {
                    var ageMins = (DateTime.UtcNow - patchTime).TotalMinutes;
                    if (ageMins > _hotFileThresholdMinutes)
                    {
                        // File is still Dirty after Hot timeout
                        _fileStates[filePath] = FileState.Dirty;
                        return FileState.Dirty;
                    }
                }

                return state;
            }

            return FileState.Clean; // Default for unknown files
        }

        /// <summary>
        /// Get a scoring boost for context ranking based on file state.
        /// Higher scores = higher priority in context selection.
        /// 
        /// Scoring:
        /// - Hot: +100 (prioritize recently patched files)
        /// - Dirty: +50 (include but flag as unstable)
        /// - Clean: 0 (normal semantic scoring applies)
        /// </summary>
        public int GetStateBoost(string filePath)
        {
            var state = GetState(filePath);
            return state switch
            {
                FileState.Hot => 100,
                FileState.Dirty => 50,
                FileState.Clean => 0,
                _ => 0
            };
        }

        /// <summary>
        /// Get a flag indicating if file state should be communicated to LLM.
        /// Returns string like "[HOT]", "[DIRTY]", or null for clean files.
        /// </summary>
        public string? GetStateFlag(string filePath)
        {
            var state = GetState(filePath);
            return state switch
            {
                FileState.Hot => "[HOT: Recently Patched]",
                FileState.Dirty => "[DIRTY: Needs Re-embedding]",
                FileState.Clean => null,
                _ => null
            };
        }

        /// <summary>
        /// Get the most relevant recency timestamp for ranking.
        /// Prefers patch time, then modified time, then DateTime.MinValue.
        /// This is additive metadata only and does not change state semantics.
        /// </summary>
        public DateTime GetLastActivityUtc(string filePath)
        {
            if (_lastPatchedTime.TryGetValue(filePath, out var patchedAt))
                return patchedAt;

            if (_lastModifiedTime.TryGetValue(filePath, out var modifiedAt))
                return modifiedAt;

            return DateTime.MinValue;
        }

        /// <summary>
        /// Clear all state tracking (for new session or reset).
        /// </summary>
        public void Clear()
        {
            _fileStates.Clear();
            _lastModifiedTime.Clear();
            _lastPatchedTime.Clear();
        }

        /// <summary>
        /// Get count of files in each state (for diagnostics).
        /// </summary>
        public (int Clean, int Dirty, int Hot) GetStateCounts()
        {
            var clean = _fileStates.Values.Count(s => s == FileState.Clean || !_fileStates.ContainsKey(_fileStates.FirstOrDefault(kvp => kvp.Value == FileState.Clean).Key ?? ""));
            var dirty = _fileStates.Values.Count(s => s == FileState.Dirty);
            var hot = _fileStates.Values.Count(s => s == FileState.Hot);

            return (clean, dirty, hot);
        }

        /// <summary>
        /// Initialize all indexed files as Clean.
        /// Called at start of indexing to establish baseline state.
        /// </summary>
        public void InitializeFilesAsClean(IEnumerable<string> filePaths)
        {
            foreach (var filePath in filePaths)
            {
                if (!_fileStates.ContainsKey(filePath))
                {
                    MarkClean(filePath);
                }
            }
        }
    }
}