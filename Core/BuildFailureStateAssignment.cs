namespace LocalCursorAgent.Core
{
    internal static class BuildFailureStateAssignment
    {
        public static (int? ExitCode, bool? TimedOut, bool? ErrorMessageTruncated, int? ErrorMessageLength) ToTuple(BuildFailureStateSnapshot state) =>
            (state.ExitCode, state.TimedOut, state.ErrorMessageTruncated, state.ErrorMessageLength);
    }
}
