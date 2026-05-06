using LocalCursorAgent.Execution;

namespace LocalCursorAgent.Core
{
    internal static class BuildFailureStateUpdater
    {
        internal static BuildFailureStateSnapshot From(BuildVerifier.BuildResult buildResult, BuildFailureMessageResolution failureMessage, string errorMessage)
        {
            return new BuildFailureStateSnapshot
            {
                ExitCode = buildResult.ExitCode,
                TimedOut = buildResult.TimedOut,
                ErrorMessageTruncated = failureMessage.IsTruncated,
                ErrorMessageLength = errorMessage.Length
            };
        }
    }

    internal sealed class BuildFailureStateSnapshot
    {
        public int? ExitCode { get; init; }
        public bool? TimedOut { get; init; }
        public bool? ErrorMessageTruncated { get; init; }
        public int? ErrorMessageLength { get; init; }
    }
}
