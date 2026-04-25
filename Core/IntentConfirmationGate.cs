using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Security;

namespace LocalCursorAgent.Core;

public enum IntentConfirmationGateOutcome
{
    Confirmed,
    Rejected,
    Bypassed
}

public sealed class IntentConfirmationGateResult
{
    public IntentConfirmationGateOutcome Outcome { get; init; }
    public string RawIntent { get; init; } = string.Empty;
    public string ClassifiedKind { get; init; } = "Unknown";
    public bool MutationLike { get; init; }
    public bool TargetConfirmed { get; init; }
    public string ReasonCode { get; init; } = Security.PermissionReasonCodes.IntentBypassed;
    public string Reason { get; init; } = string.Empty;
    public string ResolvedTarget { get; init; } = string.Empty;
    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();

    public bool IsConfirmed => Outcome == IntentConfirmationGateOutcome.Confirmed;
    public bool IsRejected => Outcome == IntentConfirmationGateOutcome.Rejected;
    public bool IsBypassed => Outcome == IntentConfirmationGateOutcome.Bypassed;
}

public sealed class IntentConfirmationGate
{
    private static readonly string[] MutationKeywords =
    {
        "add validation",
        "rename method",
        "patch exact file",
        "fix compile error",
        "fix build error",
        "update file",
        "modify file",
        "write:",
        "patch:",
        "edit:",
        "change:"
    };

    private readonly ExecutionTracer _tracer;

    public IntentConfirmationGate(ExecutionTracer tracer)
    {
        _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
    }

    public IntentConfirmationGateResult Evaluate(string task, string toolInput, TargetResolutionGateResult targetResolution)
    {
        _tracer.LogActionEvent("IntentGate", "IntentConfirmationGate", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
        {
            { "task", task },
            { "tool_input", toolInput }
        });
        var rawIntent = $"{task} {toolInput}".Trim();
        var mutationLike = IsMutationLike(toolInput, task);
        var classifiedKind = ClassifyIntent(rawIntent);

        if (!mutationLike)
        {
            return LogAndReturn(new IntentConfirmationGateResult
            {
                Outcome = IntentConfirmationGateOutcome.Bypassed,
                RawIntent = rawIntent,
                ClassifiedKind = classifiedKind,
                MutationLike = false,
                TargetConfirmed = false,
                ReasonCode = Security.PermissionReasonCodes.IntentBypassed,
                Reason = "Non-mutation intent bypassed confirmation gate.",
                ResolvedTarget = targetResolution.SelectedFiles.FirstOrDefault() ?? string.Empty
            });
        }

        var explicitTarget = ExtractExplicitTargetFromToolInput(toolInput);
        var resolvedTarget = targetResolution.SelectedFiles.FirstOrDefault() ?? explicitTarget ?? string.Empty;
        var targetConfirmed = (targetResolution.IsResolved && !string.IsNullOrWhiteSpace(resolvedTarget)) 
                           || (!string.IsNullOrWhiteSpace(explicitTarget));
        var evidence = BuildEvidence(task, toolInput, targetResolution, resolvedTarget);

        if (!targetConfirmed)
        {
            return LogAndReturn(new IntentConfirmationGateResult
            {
                Outcome = IntentConfirmationGateOutcome.Rejected,
                RawIntent = rawIntent,
                ClassifiedKind = classifiedKind,
                MutationLike = true,
                TargetConfirmed = false,
                ReasonCode = Security.PermissionReasonCodes.IntentMutationNotConfirmed,
                Reason = "Mutation intent requires a confirmed target.",
                ResolvedTarget = resolvedTarget,
                Evidence = evidence
            });
        }

        if (IsTooVague(task, toolInput))
        {
            return LogAndReturn(new IntentConfirmationGateResult
            {
                Outcome = IntentConfirmationGateOutcome.Rejected,
                RawIntent = rawIntent,
                ClassifiedKind = classifiedKind,
                MutationLike = true,
                TargetConfirmed = true,
                ReasonCode = Security.PermissionReasonCodes.IntentTooVague,
                Reason = "Mutation intent is too vague.",
                ResolvedTarget = resolvedTarget,
                Evidence = evidence
            });
        }

        if (HasTargetMismatch(task, toolInput, targetResolution))
        {
            return LogAndReturn(new IntentConfirmationGateResult
            {
                Outcome = IntentConfirmationGateOutcome.Rejected,
                RawIntent = rawIntent,
                ClassifiedKind = classifiedKind,
                MutationLike = true,
                TargetConfirmed = true,
                ReasonCode = Security.PermissionReasonCodes.IntentTargetMismatch,
                Reason = "Mutation intent conflicts with resolved target.",
                ResolvedTarget = resolvedTarget,
                Evidence = evidence
            });
        }

        if (IsScopeUnclear(task, toolInput))
        {
            return LogAndReturn(new IntentConfirmationGateResult
            {
                Outcome = IntentConfirmationGateOutcome.Rejected,
                RawIntent = rawIntent,
                ClassifiedKind = classifiedKind,
                MutationLike = true,
                TargetConfirmed = true,
                ReasonCode = Security.PermissionReasonCodes.IntentScopeUnclear,
                Reason = "Mutation scope is unclear.",
                ResolvedTarget = resolvedTarget,
                Evidence = evidence
            });
        }

        return LogAndReturn(new IntentConfirmationGateResult
        {
            Outcome = IntentConfirmationGateOutcome.Confirmed,
            RawIntent = rawIntent,
            ClassifiedKind = classifiedKind,
            MutationLike = true,
            TargetConfirmed = true,
            ReasonCode = Security.PermissionReasonCodes.IntentConfirmed,
            Reason = "Mutation intent confirmed.",
            ResolvedTarget = resolvedTarget,
            Evidence = evidence
        });
    }

    private IntentConfirmationGateResult LogAndReturn(IntentConfirmationGateResult result)
    {
        _tracer.LogIntentConfirmationGate(
            result.RawIntent,
            result.ClassifiedKind,
            result.MutationLike,
            result.TargetConfirmed,
            result.Outcome.ToString(),
            result.ReasonCode,
            result.Reason,
            result.ResolvedTarget,
            result.Evidence);
        _tracer.LogActionEvent("IntentGate", "IntentConfirmationGate", result.IsRejected ? ExecutionTracer.ActionLogLevel.Warning : ExecutionTracer.ActionLogLevel.Info, result.Outcome.ToString().ToLowerInvariant(), result.ReasonCode, new Dictionary<string, object?>
        {
            { "classified_kind", result.ClassifiedKind },
            { "mutation_like", result.MutationLike },
            { "target_confirmed", result.TargetConfirmed },
            { "resolved_target", result.ResolvedTarget },
            { "evidence", result.Evidence.ToArray() }
        });

        return result;
    }

    private static bool IsMutationLike(string toolInput, string task)
    {
        var text = string.Join(" ", new[] { toolInput, task }).ToLowerInvariant();
        if (text.Contains("write:") || text.Contains("patch:") || text.Contains("edit:") || text.Contains("change:"))
            return true;

        return MutationKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTooVague(string task, string toolInput)
    {
        var text = string.Join(" ", new[] { task, toolInput }).ToLowerInvariant();
        if (text.Length < 12)
            return true;

        if (IsConcreteSingleFileMutationTask(text))
            return false;

        if (Regex.IsMatch(text, @"\b(fix|update|change|modify|adjust|refactor|improve)\b") &&
            !Regex.IsMatch(text, @"\b(add validation|rename method|compile error|build error|exact file|target file)\b"))
        {
            return true;
        }

        return false;
    }

    private static bool HasTargetMismatch(string task, string toolInput, TargetResolutionGateResult targetResolution)
    {
        var explicitTarget = ExtractExplicitTargetFromToolInput(toolInput);
        var resolved = targetResolution.SelectedFiles.FirstOrDefault() ?? explicitTarget ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(resolved))
            return true;
            
        // Если у нас явный target из write команды - пропускаем проверку несовпадения
        if (!string.IsNullOrWhiteSpace(explicitTarget))
            return false;

        var text = string.Join(" ", new[] { task, toolInput }).ToLowerInvariant();
        var targetTokens = new[]
        {
            Path.GetFileNameWithoutExtension(resolved).ToLowerInvariant(),
            Path.GetFileName(resolved).ToLowerInvariant(),
            targetResolution.RawTargetToken.ToLowerInvariant()
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();

        var mentionsTarget = targetTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
        var mentionsOtherFile = Regex.Matches(text, @"([A-Za-z0-9_\-./\\]+\.cs)\b")
            .Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Any(candidate => !targetTokens.Any(token => string.Equals(candidate, token, StringComparison.OrdinalIgnoreCase)));

        return mentionsOtherFile || (!mentionsTarget && IsMutationLike(toolInput, task));
    }

    private static bool IsScopeUnclear(string task, string toolInput)
    {
        var text = string.Join(" ", new[] { task, toolInput }).ToLowerInvariant();
        if (text.Contains("somewhere") || text.Contains("as needed") || text.Contains("when appropriate"))
            return true;

        return Regex.IsMatch(text, @"\b(fix|update|change|modify|adjust)\b") &&
               !Regex.IsMatch(text, @"\b(file|method|class|target|exact|specific)\b");
    }

    private static bool IsConcreteSingleFileMutationTask(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var fileMentioned = Regex.IsMatch(text, @"\bprogram\.cs\b|\b[a-z0-9_\-./\\]+\.cs\b", RegexOptions.IgnoreCase);
        var oneFileHint = Regex.IsMatch(text, @"\b(one file only|exactly one file|single file|only file|overwrite\s+program\.cs|replace\s+program\.cs|target\s+program\.cs|write\s+program\.cs|edit\s+program\.cs)\b", RegexOptions.IgnoreCase);
        var concreteEditHint = Regex.IsMatch(text, @"\b(overwrite|replace|implement|write|edit|concrete code|replace scaffold|minimal runnable|direct mutation)\b", RegexOptions.IgnoreCase);

        return fileMentioned && oneFileHint && concreteEditHint;
    }

    private static string ClassifyIntent(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("validation"))
            return "Validation";
        if (lower.Contains("rename"))
            return "Rename";
        if (lower.Contains("compile error") || lower.Contains("build error"))
            return "BuildFix";
        if (lower.Contains("refactor"))
            return "Refactor";
        if (lower.Contains("fix") || lower.Contains("bug") || lower.Contains("error"))
            return "BugFix";
        if (lower.Contains("add") || lower.Contains("new") || lower.Contains("implement"))
            return "FeatureAdd";
        if (lower.Contains("update") || lower.Contains("change") || lower.Contains("modify") || lower.Contains("adjust"))
            return "Update";
        return "Unknown";
    }

    private static IReadOnlyList<string> BuildEvidence(string task, string toolInput, TargetResolutionGateResult targetResolution, string resolvedTarget)
    {
        var evidence = new List<string>();
        if (!string.IsNullOrWhiteSpace(task))
            evidence.Add($"task:{task}");
        if (!string.IsNullOrWhiteSpace(toolInput))
            evidence.Add($"tool:{toolInput}");
        if (!string.IsNullOrWhiteSpace(resolvedTarget))
            evidence.Add($"target:{resolvedTarget}");
        if (!string.IsNullOrWhiteSpace(targetResolution.ReasonCode))
            evidence.Add($"targetReason:{targetResolution.ReasonCode}");
        return evidence;
    }

    private static string? ExtractExplicitTargetFromToolInput(string toolInput)
    {
        if (string.IsNullOrWhiteSpace(toolInput))
            return null;

        var lower = toolInput.ToLowerInvariant();
        
        if (lower.StartsWith("write:") || lower.StartsWith("patch:") || lower.StartsWith("edit:"))
        {
            var payload = toolInput.Substring(toolInput.IndexOf(':') + 1);
            if (payload.Length >= 3 && payload[1] == ':' && (payload[2] == '\\' || payload[2] == '/'))
            {
                var separatorIdx = payload.IndexOf(':', 3);
                return separatorIdx >= 0 ? payload.Substring(0, separatorIdx).Trim() : payload.Trim();
            }
            
            var firstColon = payload.IndexOf(':');
            return firstColon >= 0 ? payload.Substring(0, firstColon).Trim() : payload.Trim();
        }

        return null;
    }
}
