namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private static bool TryBuildRepeatedFailureDiagnostic(
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

        private static StructuredDiagnostic BuildRepeatedFailureDiagnosticPayload(string attemptedFix, string repeatedBuildFailure)
        {
            return new StructuredDiagnostic
            {
                RootCause = "The same build failure repeated after a fix attempt.",
                AttemptedFix = attemptedFix,
                WhyDenied = repeatedBuildFailure,
                NextSafeAction = "Inspect the compiler error and regenerate one targeted edit that directly addresses it."
            };
        }
    }
}
