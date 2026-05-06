namespace LocalCursorAgent.Core
{
    internal static class RepeatedBuildFailureStructuredDiagnosticBuilder
    {
        public static string RootCause => "The same build failure repeated after a fix attempt.";

        public static string NextSafeAction =>
            "Inspect the compiler error and regenerate one targeted edit that directly addresses it.";
    }
}
