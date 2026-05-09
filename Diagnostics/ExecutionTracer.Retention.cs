using System;
using System.IO;
using System.Linq;

namespace LocalCursorAgent.Diagnostics;

public partial class ExecutionTracer
{
    private void PruneRuntimeArtifacts()
    {
        try
        {
            PruneRunArtifacts(maxRunsToKeep: 25);
            TrimJsonLinesFile(_catalogFile, maxLines: 200);
            TrimJsonLinesFile(Path.Combine(_memoryDirectory, "failure_memory.jsonl"), maxLines: 200);
            TrimJsonLinesFile(Path.Combine(_memoryDirectory, "success_memory.jsonl"), maxLines: 200);
            TrimTextFile(_traceFile, maxChars: 250_000);
            TrimTextFile(_timelineFile, maxChars: 250_000);
        }
        catch
        {
            // Silent fail
        }
    }

    private void PruneRunArtifacts(int maxRunsToKeep)
    {
        if (!Directory.Exists(_runsDirectory))
            return;

        var manifestFiles = Directory.GetFiles(_runsDirectory, "*.manifest.json")
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return !name.StartsWith("latest_", StringComparison.OrdinalIgnoreCase);
            })
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        foreach (var staleManifest in manifestFiles.Skip(maxRunsToKeep))
        {
            var runPrefix = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(staleManifest.Name));
            TryDeleteIfExists(staleManifest.FullName);
            TryDeleteIfExists(Path.Combine(_runsDirectory, $"{runPrefix}.events.jsonl"));
            TryDeleteIfExists(Path.Combine(_runsDirectory, $"{runPrefix}.summary.json"));
            TryDeleteIfExists(Path.Combine(_runsDirectory, $"{runPrefix}.summary.txt"));
            TryDeleteIfExists(Path.Combine(_runsDirectory, $"{runPrefix}.timeline.log"));
        }
    }

    private static void TrimJsonLinesFile(string path, int maxLines)
    {
        if (!File.Exists(path))
            return;

        var lines = File.ReadAllLines(path);
        if (lines.Length <= maxLines)
            return;

        File.WriteAllLines(path, lines.Skip(lines.Length - maxLines));
    }

    private static void TrimTextFile(string path, int maxChars)
    {
        if (!File.Exists(path))
            return;

        var content = File.ReadAllText(path);
        if (content.Length <= maxChars)
            return;

        File.WriteAllText(path, content[^maxChars..]);
    }

    private static void TryDeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
