using LocalCursorAgent.Execution;
using LocalCursorAgent.Memory;

namespace LocalCursorAgent.Core
{
    internal static class BuildFailureMemoryRecorder
    {
        internal static void Record(
            MemoryStore memory,
            BuildVerifier.BuildResult buildResult,
            string buildFailureCode,
            BuildFailureMessageResolution failureMessage,
            string errorMessage)
        {
            memory.Add(BuildFailureMemoryKeys.ErrorMessage, errorMessage, BuildFailureMemoryKeys.Category);
            memory.Add(BuildFailureMemoryKeys.FailureCode, buildFailureCode, BuildFailureMemoryKeys.Category);
            memory.Add(BuildFailureMemoryKeys.ExitCode, buildResult.ExitCode.ToString(), BuildFailureMemoryKeys.Category);
            memory.Add(BuildFailureMemoryKeys.TimedOut, buildResult.TimedOut ? "true" : "false", BuildFailureMemoryKeys.Category);
            memory.Add(BuildFailureMemoryKeys.MessageTruncated, failureMessage.IsTruncated ? "true" : "false", BuildFailureMemoryKeys.Category);
            memory.Add(BuildFailureMemoryKeys.MessageLength, errorMessage.Length.ToString(), BuildFailureMemoryKeys.Category);
        }
    }
}
