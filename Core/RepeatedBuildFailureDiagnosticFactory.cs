namespace LocalCursorAgent.Core
{
    internal static class RepeatedBuildFailureDiagnosticFactory
    {
        public static bool TryCreate(
            string? lastBuildErrorSignature,
            string? lastBuildFailureCode,
            string currentErrorMessage,
            out string structuredBuildFailureCode,
            out string repeatedBuildFailure)
        {
            structuredBuildFailureCode = string.Empty;
            repeatedBuildFailure = string.Empty;

            if (!string.Equals(lastBuildErrorSignature, currentErrorMessage, StringComparison.Ordinal))
            {
                return false;
            }

            structuredBuildFailureCode = BuildFailureReasonCodeMapper.ToStructuredReasonCode(lastBuildFailureCode ?? string.Empty);
            repeatedBuildFailure = string.IsNullOrWhiteSpace(lastBuildFailureCode)
                ? currentErrorMessage
                : $"[{lastBuildFailureCode}] {currentErrorMessage}";

            return true;
        }
    }
}
