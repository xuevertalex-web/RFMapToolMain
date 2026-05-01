namespace LocalCursorAgent.Core
{
    internal static class ResponseLanguageHelper
    {
        public static string BuildResponseLanguageRule(string task)
        {
            return ContainsCyrillic(task)
                ? "- Answer in Russian. The user wrote in Russian, so the final response must be in Russian."
                : "- Answer in the same language as the user's task.";
        }

        public static bool ContainsCyrillic(string value)
        {
            return !string.IsNullOrEmpty(value) && value.Any(ch => ch >= '\u0400' && ch <= '\u04FF');
        }
    }
}
