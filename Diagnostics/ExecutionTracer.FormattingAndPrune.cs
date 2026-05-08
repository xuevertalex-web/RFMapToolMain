using System;
using System.Collections.Generic;
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

    private static string BuildReadableRunSummary(RunManifest manifest)
    {
        var changedFiles = manifest.ChangedFiles.Length == 0
            ? "none"
            : string.Join(Environment.NewLine, manifest.ChangedFiles.Select(path => $"- {path}"));
        var degraded = manifest.DegradedFlags.Count == 0
            ? "none"
            : string.Join(", ", manifest.DegradedFlags.Where(x => x.Value).Select(x => x.Key));

        return string.Join(Environment.NewLine, new[]
        {
            $"Run: {manifest.RunId}",
            $"Status: {manifest.FinalStatus}",
            $"Reason: {manifest.ReasonCode}",
            $"Started: {manifest.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC",
            $"Completed: {manifest.CompletedAtUtc:yyyy-MM-dd HH:mm:ss} UTC",
            $"Duration: {Math.Round(manifest.DurationMs)} ms",
            $"Workspace: {manifest.WorkspaceRoot}",
            $"Access: {manifest.AccessMode}",
            $"Provider/Model: {manifest.Provider} / {manifest.Model}",
            $"Embeddings: {manifest.EmbeddingsStatus}",
            $"Indexing: {manifest.IndexingStatus}",
            $"Build succeeded: {manifest.BuildSucceeded}",
            $"Stop-point: {(string.IsNullOrWhiteSpace(manifest.StopPoint) ? "none" : manifest.StopPoint)}",
            $"Cancel source: {(string.IsNullOrWhiteSpace(manifest.CancelSource) ? "none" : manifest.CancelSource)}",
            $"Degraded flags: {degraded}",
            $"Event count: {manifest.EventCount}",
            "",
            "Task:",
            manifest.TaskNormalized,
            "",
            "Summary:",
            string.IsNullOrWhiteSpace(manifest.Summary) ? "none" : manifest.Summary,
            "",
            "Changed files:",
            changedFiles
        });
    }

    private static string FormatExecutionLogEntry(ExecutionLogEntry entry)
    {
        var details = FlattenMetadata(entry.Details.ToDictionary(x => x.Key, x => (object?)x.Value));
        var detailText = string.IsNullOrWhiteSpace(details) ? string.Empty : $" | {details}";
        var outcome = string.IsNullOrWhiteSpace(entry.Outcome) ? string.Empty : $" | outcome={entry.Outcome}";
        var duration = entry.Duration is null ? string.Empty : $" | duration={Math.Round(entry.Duration.Value)}ms";
        var message = string.IsNullOrWhiteSpace(entry.Message) ? entry.EventType : entry.Message;
        return $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {entry.EventType} | {message}{outcome}{duration}{detailText}";
    }

    private static string FormatActionEvent(ActionEvent entry)
    {
        var metadata = FlattenMetadata(entry.Metadata);
        var reason = string.IsNullOrWhiteSpace(entry.ReasonCode) ? string.Empty : $" | reason={entry.ReasonCode}";
        var duration = entry.DurationMs is null ? string.Empty : $" | duration={entry.DurationMs}ms";
        var correlation = string.IsNullOrWhiteSpace(entry.CorrelationId) ? string.Empty : $" | corr={entry.CorrelationId}";
        var metadataText = string.IsNullOrWhiteSpace(metadata) ? string.Empty : $" | {metadata}";
        return $"[{entry.Sequence:0000}] [{entry.TimestampUtc:HH:mm:ss.fff}] {entry.Component}/{entry.EventType} | {entry.Level} | {entry.Outcome}{reason}{duration}{correlation}{metadataText}";
    }

    private static string FlattenMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        if (metadata.Count == 0)
            return string.Empty;

        var parts = new List<string>();
        foreach (var pair in metadata.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var formatted = FormatMetadataValue(pair.Value);
            if (!string.IsNullOrWhiteSpace(formatted))
                parts.Add($"{pair.Key}={formatted}");
        }

        return string.Join(" | ", parts);
    }

    private static string FormatMetadataValue(object? value)
    {
        if (value is null)
            return string.Empty;

        if (value is string text)
        {
            text = text.Replace(Environment.NewLine, " ").Trim();
            if (text.Length > 140)
                text = text[..137] + "...";
            return text;
        }

        if (value is Array array)
        {
            var items = array.Cast<object?>().Take(3).Select(FormatMetadataValue).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            return array.Length switch
            {
                0 => "[]",
                <= 3 => $"[{string.Join(", ", items)}]",
                _ => $"[{string.Join(", ", items)}, ...] ({array.Length} items)"
            };
        }

        if (value is System.Collections.IDictionary dictionary)
        {
            var items = new List<string>();
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                if (items.Count == 3)
                    break;
                items.Add($"{entry.Key}:{FormatMetadataValue(entry.Value)}");
            }

            return dictionary.Count switch
            {
                0 => "{}",
                <= 3 => $"{{{string.Join(", ", items)}}}",
                _ => $"{{{string.Join(", ", items)}, ...}} ({dictionary.Count} keys)"
            };
        }

        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }
}