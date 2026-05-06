using LocalCursorAgent.Execution;

namespace LocalCursorAgent.Core
{
    internal static class BuildFailureMessageResolver
    {
        internal static string Resolve(BuildVerifier.BuildResult buildResult, string buildFailureCode)
        {
            var errorMessage = string.Join("\n", buildResult.Errors.Where(e => !string.IsNullOrWhiteSpace(e)));
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                return errorMessage;
            }

            if (!string.IsNullOrWhiteSpace(buildResult.FullOutput))
            {
                return buildResult.FullOutput;
            }

            return string.IsNullOrWhiteSpace(buildFailureCode) ? "Build failed with no diagnostic output." : $"Build failed: {buildFailureCode}";
        }
    }
}
