using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    internal static class ChangedHintComposer
    {
        public static Agent.ChangedHint BuildChangedHint(string filePath, string toolInput, ExecutionTracer.PatchDecision patchDecision)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var reasonHint = ChangeHintBuilder.NormalizeHint(patchDecision.Reason);
            if (!string.IsNullOrWhiteSpace(reasonHint))
                return new Agent.ChangedHint { File = filePath, Hint = reasonHint };

            var actionHint = ChangeHintBuilder.ExtractActionHint(toolInput, fileName);
            if (!string.IsNullOrWhiteSpace(actionHint))
                return new Agent.ChangedHint { File = filePath, Hint = actionHint };

            return new Agent.ChangedHint { File = filePath, Hint = "Updated by agent" };
        }
    }
}
