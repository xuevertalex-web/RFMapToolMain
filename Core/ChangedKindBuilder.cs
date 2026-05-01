using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    internal static class ChangedKindBuilder
    {
        public static Agent.ChangedKind BuildChangedKind(
            string task,
            string toolInput,
            ExecutionTracer.PatchDecision patchDecision,
            Execution.BuildVerifier.BuildResult? buildResult)
        {
            var intent = ChangedKindClassifier.ClassifyIntent(task, toolInput, patchDecision.Reason, buildResult);
            return new Agent.ChangedKind
            {
                File = patchDecision.TargetFile,
                Kind = intent.ToString()
            };
        }
    }
}
