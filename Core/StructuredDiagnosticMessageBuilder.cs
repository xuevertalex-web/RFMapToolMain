namespace LocalCursorAgent.Core
{
    internal static class StructuredDiagnosticMessageBuilder
    {
        public static string Build(Agent.StructuredDiagnostic diagnostic)
        {
            return $@"Structured diagnostic:
root_cause: {diagnostic.RootCause}
attempted_fix: {diagnostic.AttemptedFix}
why_denied: {diagnostic.WhyDenied}
next_safe_action: {diagnostic.NextSafeAction}";
        }
    }
}
