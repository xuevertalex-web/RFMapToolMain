namespace LocalCursorAgent.Core
{
    internal static class BuildFailureReasonCodeMapper
    {
        internal static string ToStructuredReasonCode(string buildFailureCode)
        {
            return buildFailureCode switch
            {
                "BUILD_TIMEOUT" => "BUILD_TIMEOUT",
                "BUILD_COMPILER_ERRORS" => "BUILD_COMPILER_ERRORS",
                "BUILD_INVALID_WORKDIR" => "BUILD_INVALID_WORKDIR",
                "BUILD_FAILED_UNKNOWN" => "BUILD_FAILED_UNKNOWN",
                _ => string.IsNullOrWhiteSpace(buildFailureCode) ? "BUILD_FAILED" : buildFailureCode
            };
        }
    }
}
