namespace LocalCursorAgent.Core
{
    internal static class EffectiveReasonCodeResolver
    {
        internal static string Resolve(string? failureReasonCode, string reasonCode)
        {
            return (failureReasonCode ?? reasonCode) ?? string.Empty;
        }
    }
}
