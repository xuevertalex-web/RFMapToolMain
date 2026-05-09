using System.Collections.Generic;
using System.Linq;

namespace LocalCursorAgent.Diagnostics;

public partial class ExecutionTracer
{
    public void LogTargetResolution(string query, string targetToken, IReadOnlyList<string> symbolCandidates, IReadOnlyList<string> filenameCandidates, IReadOnlyList<string> selectedFiles, bool safeFailure, string? failureMessage)
    {
        LogEvent("TargetResolution", "Resolved task target", new Dictionary<string, object>
        {
            { "Query", query },
            { "TargetToken", targetToken },
            { "SymbolCandidates", symbolCandidates.ToArray() },
            { "FilenameCandidates", filenameCandidates.ToArray() },
            { "SelectedFiles", selectedFiles.ToArray() },
            { "SafeFailure", safeFailure },
            { "FailureMessage", failureMessage ?? string.Empty }
        });
    }

    public void LogTargetResolutionGate(string query, string rawTargetToken, string classification, IReadOnlyList<string> exactSymbolCandidates, IReadOnlyList<string> exactFilenameCandidates, IReadOnlyList<string> partialCandidates, IReadOnlyList<string> semanticCandidates, IReadOnlyList<string> selectedFiles, string outcome, string reasonCode, string reason, double confidence)
    {
        LogEvent("TargetResolutionGate", "Evaluated exact target gate", new Dictionary<string, object>
        {
            { "Query", query },
            { "RawTargetToken", rawTargetToken },
            { "Classification", classification },
            { "ExactSymbolCandidates", exactSymbolCandidates.ToArray() },
            { "ExactFilenameCandidates", exactFilenameCandidates.ToArray() },
            { "PartialCandidates", partialCandidates.ToArray() },
            { "SemanticCandidates", semanticCandidates.ToArray() },
            { "SelectedFiles", selectedFiles.ToArray() },
            { "Outcome", outcome },
            { "ReasonCode", reasonCode },
            { "Reason", reason },
            { "Confidence", confidence }
        });
    }

    public void LogIntentConfirmationGate(string rawIntent, string classifiedKind, bool mutationLike, bool targetConfirmed, string outcome, string reasonCode, string reason, string resolvedTarget, IReadOnlyList<string> evidence)
    {
        LogEvent("IntentConfirmationGate", "Evaluated first actionable intent", new Dictionary<string, object>
        {
            { "RawIntent", rawIntent },
            { "ClassifiedKind", classifiedKind },
            { "MutationLike", mutationLike },
            { "TargetConfirmed", targetConfirmed },
            { "Outcome", outcome },
            { "ReasonCode", reasonCode },
            { "Reason", reason },
            { "ResolvedTarget", resolvedTarget },
            { "Evidence", evidence.ToArray() }
        });
    }

    public void LogMultiFileEditGate(string rawIntent, string classifiedKind, bool explicitMultiFile, bool intentConfirmed, IReadOnlyList<string> plannedMutationFiles, IReadOnlyList<string> confirmedTargetFiles, string outcome, string reasonCode, string reason)
    {
        LogEvent("MultiFileEditGate", "Evaluated multi-file mutation intent", new Dictionary<string, object>
        {
            { "RawIntent", rawIntent },
            { "ClassifiedKind", classifiedKind },
            { "ExplicitMultiFile", explicitMultiFile },
            { "IntentConfirmed", intentConfirmed },
            { "PlannedMutationFiles", plannedMutationFiles.ToArray() },
            { "ConfirmedTargetFiles", confirmedTargetFiles.ToArray() },
            { "Outcome", outcome },
            { "ReasonCode", reasonCode },
            { "Reason", reason }
        });
    }
}
