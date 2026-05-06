using LocalCursorAgent.Execution;

namespace LocalCursorAgent.Core
{
    internal static class BuildFailureMessageResolver
    {
        private const int MaxFailureMessageLength = 4000;

        internal static string Resolve(BuildVerifier.BuildResult buildResult, string buildFailureCode)
        {
            var errorMessage = string.Join("\n", buildResult.Errors.Where(e => !string.IsNullOrWhiteSpace(e)));
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                return Truncate(errorMessage);
            }

            if (!string.IsNullOrWhiteSpace(buildResult.FullOutput))
            {
                return Truncate(buildResult.FullOutput);
            }

            var fallback = string.IsNullOrWhiteSpace(buildFailureCode) ? "Build failed with no diagnostic output." : $"Build failed: {buildFailureCode}";
            return Truncate(fallback);
        }

        private static string Truncate(string value)
        {
            if (value.Length <= MaxFailureMessageLength)
            {
                return value;
            }

            return value.Substring(0, MaxFailureMessageLength) + "\n...<truncated>";
        }
    }
}
