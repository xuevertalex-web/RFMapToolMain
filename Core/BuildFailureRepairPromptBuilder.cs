namespace LocalCursorAgent.Core
{
    internal static class BuildFailureRepairPromptBuilder
    {
        public static string Build(string buildFailureCode, string errorMessage) =>
            $"Build errors encountered ({buildFailureCode}):\n{errorMessage}\n\nPlease fix these errors.";
    }
}
