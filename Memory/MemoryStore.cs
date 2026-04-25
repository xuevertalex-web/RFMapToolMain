using LocalCursorAgent.Security;

namespace LocalCursorAgent.Memory
{
    /// <summary>
    /// Stores conversation and execution history for the agent.
    /// </summary>
    public class MemoryStore
    {
        private readonly List<MemoryEntry> _entries = new();

        public class MemoryEntry
        {
            public DateTime Timestamp { get; set; }
            public string Type { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string? Reason { get; set; }
        }

        /// <summary>
        /// Add an entry to memory.
        /// </summary>
        public void Add(string type, string content)
        {
            Add(type, content, null);
        }

        public void Add(string type, string content, string? reason)
        {
            _entries.Add(new MemoryEntry
            {
                Timestamp = DateTime.UtcNow,
                Type = type,
                Content = content,
                Reason = reason
            });
        }

        public void AddSessionHeader(AgentSessionContext session, IEnumerable<string> protectedRoots)
        {
            var roots = protectedRoots.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
            Add("session_header",
                $"session={session.SessionId}; runtime_root={session.RuntimeRoot}; workspace_root={session.ActiveWorkspaceRoot}; access_mode={session.AccessMode}; protected_roots=[{string.Join(", ", roots)}]",
                "SessionStart");
        }

        public void AddWorkspaceResolution(WorkspaceResolutionResult resolution, string runtimeRoot, string? seedPath)
        {
            Add("workspace_resolution",
                $"success={resolution.Success}; reason_code={resolution.ReasonCode}; reason_name={resolution.ReasonCodeName}; source={resolution.Source ?? string.Empty}; seed_path={seedPath ?? string.Empty}; runtime_root={runtimeRoot}; workspace_root={resolution.WorkspaceRoot ?? string.Empty}; message={resolution.Message}",
                resolution.Success ? "WorkspaceResolved" : "WorkspaceResolutionFailed");
        }

        /// <summary>
        /// Get all entries.
        /// </summary>
        public IEnumerable<MemoryEntry> GetAllEntries()
        {
            return _entries.ToList().AsReadOnly();
        }

        /// <summary>
        /// Get entries of a specific type.
        /// </summary>
        public IEnumerable<MemoryEntry> GetEntriesByType(string type)
        {
            return _entries.Where(e => e.Type == type).ToList().AsReadOnly();
        }

        /// <summary>
        /// Get last N entries as formatted context.
        /// </summary>
        public string GetContextString(int maxEntries = 10)
        {
            var recentEntries = _entries.TakeLast(maxEntries).ToList();

            if (recentEntries.Count == 0)
                return string.Empty;

            var contextLines = recentEntries
                .Select(FormatEntry)
                .ToList();

            return string.Join("\n", contextLines);
        }

        private static string FormatEntry(MemoryEntry entry)
        {
            if (entry.Type == "workspace_resolution")
            {
                return string.IsNullOrWhiteSpace(entry.Reason)
                    ? $"[{entry.Timestamp:HH:mm:ss}] workspace_resolution: {entry.Content}"
                    : $"[{entry.Timestamp:HH:mm:ss}] workspace_resolution [{entry.Reason}]: {entry.Content}";
            }

            if (entry.Type == "session_header")
            {
                return string.IsNullOrWhiteSpace(entry.Reason)
                    ? $"[{entry.Timestamp:HH:mm:ss}] session_header: {entry.Content}"
                    : $"[{entry.Timestamp:HH:mm:ss}] session_header [{entry.Reason}]: {entry.Content}";
            }

            return string.IsNullOrWhiteSpace(entry.Reason)
                ? $"[{entry.Timestamp:HH:mm:ss}] {entry.Type}: {entry.Content}"
                : $"[{entry.Timestamp:HH:mm:ss}] {entry.Type} [{entry.Reason}]: {entry.Content}";
        }

        /// <summary>
        /// Clear all memory.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
        }

        /// <summary>
        /// Get total number of entries.
        /// </summary>
        public int Count => _entries.Count;
    }
}
