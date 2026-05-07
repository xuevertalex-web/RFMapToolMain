namespace LocalCursorAgent.Core
{
    internal static class RepeatedBuildFailureDiagnosticPayloadBuilder
    {
        private const string RootCause = "The same build failure repeated after a fix attempt.";
        private const string NextSafeAction = "Inspect the compiler error and regenerate one targeted edit that directly addresses it.";

        public static Agent.StructuredDiagnostic Build(string attemptedFix, string repeatedBuildFailure) =>
            new()
            {
                RootCause = RootCause,
                AttemptedFix = attemptedFix,
                WhyDenied = repeatedBuildFailure,
                NextSafeAction = NextSafeAction
            };
    }
}
