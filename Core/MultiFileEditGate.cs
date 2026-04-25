using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core;

public enum MultiFileEditGateOutcome
{
    SingleFileAllowed,
    MultiFileAllowed,
    Rejected
}

public sealed class MultiFileEditGateResult
{
    public MultiFileEditGateOutcome Outcome { get; init; }
    public IReadOnlyList<string> PlannedMutationFiles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ConfirmedTargetFiles { get; init; } = Array.Empty<string>();
    public int SelectedFileCount => PlannedMutationFiles.Count;
    public string ClassifiedKind { get; init; } = "Unknown";
    public string ReasonCode { get; init; } = Security.PermissionReasonCodes.MultiFileNotConfirmed;
    public string Reason { get; init; } = string.Empty;
    public bool IsRejected => Outcome == MultiFileEditGateOutcome.Rejected;
    public bool IsSingleFileAllowed => Outcome == MultiFileEditGateOutcome.SingleFileAllowed;
    public bool IsMultiFileAllowed => Outcome == MultiFileEditGateOutcome.MultiFileAllowed;
}

public sealed class MultiFileEditGate
{
    private const int MaxSafeFileCount = 3;
    private readonly ExecutionTracer _tracer;

    public MultiFileEditGate(ExecutionTracer tracer)
    {
        _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
    }

    public MultiFileEditGateResult Evaluate(string task, IReadOnlyList<ToolCaller.ToolCall> toolCalls, TargetResolutionGateResult targetResolution, IntentConfirmationGateResult intentDecision)
    {
        _tracer.LogActionEvent("MultiFileGate", "MultiFileEditGate", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
        {
            { "task", task },
            { "tool_calls", toolCalls.Select(x => $"{x.ToolName}:{x.Input}").ToArray() }
        });
        var plannedFiles = ExtractPlannedMutationFiles(toolCalls);
        var confirmedTargets = targetResolution.SelectedFiles.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var classifiedKind = plannedFiles.Count <= 1 ? "SingleFile" : "MultiFile";
        var multiFileIntent = IsExplicitMultiFileIntent(task, toolCalls, intentDecision);
        var scopeClear = IsScopeClear(task, toolCalls, plannedFiles);

        if (plannedFiles.Count == 0)
        {
            return LogAndReturn(new MultiFileEditGateResult
            {
                Outcome = MultiFileEditGateOutcome.SingleFileAllowed,
                PlannedMutationFiles = plannedFiles,
                ConfirmedTargetFiles = confirmedTargets,
                ClassifiedKind = classifiedKind,
                ReasonCode = Security.PermissionReasonCodes.SingleFileConfirmed,
                Reason = "No file mutation targets were planned."
            }, task, multiFileIntent, confirmedTargets);
        }

        if (plannedFiles.Count == 1)
        {
            var file = plannedFiles[0];
            
            // Если у нас явный одиночный файл из write команды - всегда разрешаем
            return LogAndReturn(new MultiFileEditGateResult
            {
                Outcome = MultiFileEditGateOutcome.SingleFileAllowed,
                PlannedMutationFiles = plannedFiles,
                ConfirmedTargetFiles = confirmedTargets,
                ClassifiedKind = classifiedKind,
                ReasonCode = Security.PermissionReasonCodes.SingleFileConfirmed,
                Reason = "Single-file mutation intent confirmed."
            }, task, multiFileIntent, confirmedTargets);
        }

        if (!multiFileIntent)
        {
            return LogAndReturn(new MultiFileEditGateResult
            {
                Outcome = MultiFileEditGateOutcome.Rejected,
                PlannedMutationFiles = plannedFiles,
                ConfirmedTargetFiles = confirmedTargets,
                ClassifiedKind = classifiedKind,
                ReasonCode = Security.PermissionReasonCodes.MultiFileNotConfirmed,
                Reason = "Multi-file mutation intent was not explicitly confirmed."
            }, task, multiFileIntent, confirmedTargets);
        }

        if (!scopeClear)
        {
            return LogAndReturn(new MultiFileEditGateResult
            {
                Outcome = MultiFileEditGateOutcome.Rejected,
                PlannedMutationFiles = plannedFiles,
                ConfirmedTargetFiles = confirmedTargets,
                ClassifiedKind = classifiedKind,
                ReasonCode = Security.PermissionReasonCodes.MultiFileScopeUnclear,
                Reason = "Multi-file mutation scope is unclear."
            }, task, multiFileIntent, confirmedTargets);
        }

        if (plannedFiles.Count > MaxSafeFileCount)
        {
            return LogAndReturn(new MultiFileEditGateResult
            {
                Outcome = MultiFileEditGateOutcome.Rejected,
                PlannedMutationFiles = plannedFiles,
                ConfirmedTargetFiles = confirmedTargets,
                ClassifiedKind = classifiedKind,
                ReasonCode = Security.PermissionReasonCodes.MultiFileTooBroad,
                Reason = $"Multi-file mutation exceeds safe file threshold ({MaxSafeFileCount})."
            }, task, multiFileIntent, confirmedTargets);
        }

        if (confirmedTargets.Count == 0)
        {
            return LogAndReturn(new MultiFileEditGateResult
            {
                Outcome = MultiFileEditGateOutcome.Rejected,
                PlannedMutationFiles = plannedFiles,
                ConfirmedTargetFiles = confirmedTargets,
                ClassifiedKind = classifiedKind,
                ReasonCode = Security.PermissionReasonCodes.MultiFileTargetSetUnconfirmed,
                Reason = "Multi-file mutation target set was not confirmed."
            }, task, multiFileIntent, confirmedTargets);
        }

        if (!plannedFiles.All(file => confirmedTargets.Contains(file, StringComparer.OrdinalIgnoreCase)))
        {
            return LogAndReturn(new MultiFileEditGateResult
            {
                Outcome = MultiFileEditGateOutcome.Rejected,
                PlannedMutationFiles = plannedFiles,
                ConfirmedTargetFiles = confirmedTargets,
                ClassifiedKind = classifiedKind,
                ReasonCode = Security.PermissionReasonCodes.MultiFileTargetSetUnconfirmed,
                Reason = "Planned mutation files do not match confirmed targets."
            }, task, multiFileIntent, confirmedTargets);
        }

        return LogAndReturn(new MultiFileEditGateResult
        {
            Outcome = MultiFileEditGateOutcome.MultiFileAllowed,
            PlannedMutationFiles = plannedFiles,
            ConfirmedTargetFiles = confirmedTargets,
            ClassifiedKind = classifiedKind,
            ReasonCode = Security.PermissionReasonCodes.MultiFileConfirmed,
            Reason = "Multi-file mutation intent confirmed."
        }, task, multiFileIntent, confirmedTargets);
    }

    private MultiFileEditGateResult LogAndReturn(MultiFileEditGateResult result, string task, bool multiFileIntent, IReadOnlyList<string> confirmedTargets)
    {
        _tracer.LogMultiFileEditGate(
            task,
            result.ClassifiedKind,
            multiFileIntent,
            result.Outcome is MultiFileEditGateOutcome.SingleFileAllowed or MultiFileEditGateOutcome.MultiFileAllowed,
            result.PlannedMutationFiles,
            confirmedTargets,
            result.Outcome.ToString(),
            result.ReasonCode,
            result.Reason);
        _tracer.LogActionEvent("MultiFileGate", "MultiFileEditGate", result.IsRejected ? ExecutionTracer.ActionLogLevel.Warning : ExecutionTracer.ActionLogLevel.Info, result.Outcome.ToString().ToLowerInvariant(), result.ReasonCode, new Dictionary<string, object?>
        {
            { "planned_mutation_files", result.PlannedMutationFiles.ToArray() },
            { "confirmed_target_files", confirmedTargets.ToArray() },
            { "explicit_multi_file_intent", multiFileIntent }
        });

        return result;
    }

    private static List<string> ExtractPlannedMutationFiles(IReadOnlyList<ToolCaller.ToolCall> toolCalls)
    {
        var files = new List<string>();
        foreach (var call in toolCalls)
        {
            if (!call.ToolName.Equals("file", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!call.Input.StartsWith("write:", StringComparison.OrdinalIgnoreCase))
                continue;

            var path = ExtractWriteTargetPath(call.Input.Substring(6));
            if (!string.IsNullOrWhiteSpace(path))
                files.Add(path);
        }

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ExtractWriteTargetPath(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        if (input.Length >= 3 &&
            input[1] == ':' &&
            (input[2] == '\\' || input[2] == '/'))
        {
            var separator = input.IndexOf(':', 3);
            return separator >= 0 ? input[..separator].Trim() : input.Trim();
        }

        var idx = input.IndexOf(':');
        return idx >= 0 ? input[..idx].Trim() : input.Trim();
    }

    private static bool IsExplicitMultiFileIntent(string task, IReadOnlyList<ToolCaller.ToolCall> toolCalls, IntentConfirmationGateResult intentDecision)
    {
        var text = string.Join(" ", new[] { task, string.Join(" ", toolCalls.Select(x => x.Input)), intentDecision.RawIntent }).ToLowerInvariant();
        return text.Contains("multiple files") ||
               text.Contains("multi-file") ||
               text.Contains("multi file") ||
               text.Contains("project-wide") ||
               text.Contains("cross-file") ||
               text.Contains("across files") ||
               text.Contains("all files");
    }

    private static bool IsScopeClear(string task, IReadOnlyList<ToolCaller.ToolCall> toolCalls, IReadOnlyList<string> plannedFiles)
    {
        var text = string.Join(" ", new[] { task, string.Join(" ", toolCalls.Select(x => x.Input)) }).ToLowerInvariant();
        if (text.Contains("some files") || text.Contains("maybe") || text.Contains("as needed") || text.Contains("where necessary"))
            return false;

        return plannedFiles.Count > 0;
    }

    private static bool IsConfirmedSingleTarget(string plannedFile, IReadOnlyList<string> confirmedTargets)
    {
        if (string.IsNullOrWhiteSpace(plannedFile))
            return false;

        if (confirmedTargets.Count == 0)
            return false;

        return confirmedTargets.Any(target => PathsMatch(plannedFile, target));
    }

    private static bool PathsMatch(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        var normalizedLeft = NormalizePathForComparison(left);
        var normalizedRight = NormalizePathForComparison(right);

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase))
            return true;

        return normalizedLeft.EndsWith("/" + normalizedRight, StringComparison.OrdinalIgnoreCase) ||
               normalizedRight.EndsWith("/" + normalizedLeft, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathForComparison(string path)
    {
        return path
            .Replace('\\', '/')
            .TrimEnd('/');
    }
}
