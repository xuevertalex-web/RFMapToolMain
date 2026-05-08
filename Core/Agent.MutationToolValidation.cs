using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private bool TryValidateMutationToolCalls(
            string task,
            List<ToolCaller.ToolCall> toolCalls,
            ToolCaller.ToolCall mutationCall,
            TargetResolutionGateResult targetResolution,
            ExecutionTracer tracer,
            out string finalResult)
        {
            var intentGate = new IntentConfirmationGate(_contextBuilder.Tracer);
            var intentDecision = intentGate.Evaluate(task, mutationCall.Input, targetResolution);
            _memory.Add("intent_confirmation_gate", $"{intentDecision.ReasonCode}:{intentDecision.ClassifiedKind}:{intentDecision.Outcome}");

            if (intentDecision.IsRejected)
            {
                var safeFailure = intentDecision.Reason;
                _memory.Add("context_failure", safeFailure, intentDecision.ReasonCode);
                tracer.MarkStopPoint("IntentConfirmationGate", intentDecision.ReasonCode, safeFailure, new[] { "MultiFileGate", "PatchApply", "BuildVerification" });
                finalResult = FinalizeRunResult(
                    false,
                    safeFailure,
                    $"Intent confirmation gate failed: {intentDecision.ReasonCode}",
                    intentDecision.ReasonCode,
                    Array.Empty<string>(),
                    Array.Empty<ChangedHint>(),
                    Array.Empty<ChangedRange>(),
                    Array.Empty<ChangedKind>(),
                    false);
                return true;
            }

            var multiFileGate = new MultiFileEditGate(_contextBuilder.Tracer);
            var multiFileDecision = multiFileGate.Evaluate(task, toolCalls, targetResolution, intentDecision);
            _memory.Add("multi_file_edit_gate", $"{multiFileDecision.ReasonCode}:{multiFileDecision.ClassifiedKind}:{multiFileDecision.Outcome}");

            if (multiFileDecision.IsRejected)
            {
                var safeFailure = multiFileDecision.Reason;
                _memory.Add("context_failure", safeFailure, multiFileDecision.ReasonCode);
                tracer.MarkStopPoint("MultiFileEditGate", multiFileDecision.ReasonCode, safeFailure, new[] { "PatchApply", "BuildVerification" });
                finalResult = FinalizeRunResult(
                    false,
                    safeFailure,
                    $"Multi-file edit gate failed: {multiFileDecision.ReasonCode}",
                    multiFileDecision.ReasonCode,
                    Array.Empty<string>(),
                    Array.Empty<ChangedHint>(),
                    Array.Empty<ChangedRange>(),
                    Array.Empty<ChangedKind>(),
                    false);
                return true;
            }

            finalResult = string.Empty;
            return false;
        }
    }
}
