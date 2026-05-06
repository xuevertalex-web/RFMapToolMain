namespace LocalCursorAgent.Core
{
    internal static class VerificationOutcomeReasonCodeResolver
    {
        internal static string Resolve(string? failureReasonCode, string reasonCode)
        {
            return failureReasonCode ?? reasonCode;
        }
    }
}
