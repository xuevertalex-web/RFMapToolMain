namespace LocalCursorAgent.Core
{
    internal static class RepeatedBuildFailureDiagnosticPayloadBuilder
    {
        public static Agent.StructuredDiagnostic Build(string attemptedFix, string repeatedBuildFailure) =>
            new()
            {
                RootCause = RepeatedBuildFailureStructuredDiagnosticBuilder.RootCause,
                AttemptedFix = attemptedFix,
                WhyDenied = repeatedBuildFailure,
                NextSafeAction = RepeatedBuildFailureStructuredDiagnosticBuilder.NextSafeAction
            };
    }
}
