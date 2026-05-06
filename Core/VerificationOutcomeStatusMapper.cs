namespace LocalCursorAgent.Core
{
    internal static class VerificationOutcomeStatusMapper
    {
        internal static string ToWireValue(VerificationOutcomeStatus status)
        {
            return status.ToString();
        }
    }
}
