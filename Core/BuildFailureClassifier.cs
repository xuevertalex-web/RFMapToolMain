using LocalCursorAgent.Execution;

namespace LocalCursorAgent.Core
{
    internal static class BuildFailureClassifier
    {
        internal static string Classify(BuildVerifier.BuildResult buildResult)
        {
            if (buildResult.Success)
            {
                return "BUILD_OK";
            }

            if (buildResult.TimedOut)
            {
                return "BUILD_TIMEOUT";
            }

            if (buildResult.Errors.Any(e => e.Contains("error CS", StringComparison.OrdinalIgnoreCase)))
            {
                return "BUILD_COMPILER_ERRORS";
            }

            if (string.Equals(buildResult.ReasonCode, "INVALID_WORKING_DIRECTORY", StringComparison.OrdinalIgnoreCase))
            {
                return "BUILD_INVALID_WORKDIR";
            }

            if (!string.IsNullOrWhiteSpace(buildResult.ReasonCode))
            {
                return $"BUILD_{buildResult.ReasonCode}";
            }

            return "BUILD_FAILED_UNKNOWN";
        }
    }
}
