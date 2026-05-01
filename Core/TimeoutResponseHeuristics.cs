namespace LocalCursorAgent.Core
{
    internal static class TimeoutResponseHeuristics
    {
        public static bool IsModelTimeoutResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return false;

            return response.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                   response.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        }
    }
}
