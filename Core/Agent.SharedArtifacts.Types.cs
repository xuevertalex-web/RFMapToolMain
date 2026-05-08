namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        internal sealed class ChangedHint
        {
            public string File { get; init; } = string.Empty;
            public string Hint { get; init; } = string.Empty;
        }

        internal sealed class ChangedRange
        {
            public string File { get; init; } = string.Empty;
            public int StartLine { get; init; }
            public int EndLine { get; init; }
        }

        internal sealed class ChangedKind
        {
            public string File { get; init; } = string.Empty;
            public string Kind { get; init; } = string.Empty;
        }

        internal sealed class StructuredDiagnostic
        {
            public string RootCause { get; init; } = string.Empty;
            public string AttemptedFix { get; init; } = string.Empty;
            public string WhyDenied { get; init; } = string.Empty;
            public string NextSafeAction { get; init; } = string.Empty;
        }
    }
}
