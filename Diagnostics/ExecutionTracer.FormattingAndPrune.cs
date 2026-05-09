using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LocalCursorAgent.Diagnostics;

public partial class ExecutionTracer
{
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

}
