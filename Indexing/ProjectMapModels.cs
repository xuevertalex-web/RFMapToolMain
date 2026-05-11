namespace LocalCursorAgent.Indexing
{
    public sealed class ProjectMapSnapshot
    {
        public DateTime GeneratedAtUtc { get; init; }
        public int FileCount { get; init; }
        public List<string> Zones { get; init; } = new();
        public List<FileMapEntry> Files { get; init; } = new();
    }

    public sealed class FileMapEntry
    {
        public string Path { get; init; } = string.Empty;
        public string Zone { get; init; } = "docs/config";
        public string Role { get; init; } = "config";
        public bool IsEntrypoint { get; init; }
        public List<string> Hints { get; init; } = new();
    }
}
