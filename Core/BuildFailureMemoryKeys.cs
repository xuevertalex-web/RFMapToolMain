namespace LocalCursorAgent.Core
{
    internal static class BuildFailureMemoryKeys
    {
        internal const string ErrorMessage = "build_errors";
        internal const string FailureCode = "build_failure_code";
        internal const string ExitCode = "build_exit_code";
        internal const string TimedOut = "build_timed_out";
        internal const string MessageTruncated = "build_error_message_truncated";
        internal const string MessageLength = "build_error_message_length";
        internal const string RepeatedFailureReasonCode = "build_repeated_failure_reason_code";
        internal const string Category = "BuildVerificationFailed";
    }
}
