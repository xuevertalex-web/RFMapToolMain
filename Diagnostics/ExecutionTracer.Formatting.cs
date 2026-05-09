using System;
using System.Collections.Generic;
using System.Linq;

namespace LocalCursorAgent.Diagnostics;

public partial class ExecutionTracer
{
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
