namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class LoopDecision
        {
            public bool IsHandled { get; init; }
            public bool ShouldContinue { get; init; }
            public string Payload { get; init; } = string.Empty;

            public static LoopDecision Continue(string nextResponse) => new()
            {
                IsHandled = true,
                ShouldContinue = true,
                Payload = nextResponse
            };

            public static LoopDecision Finalize(string finalResult) => new()
            {
                IsHandled = true,
                Payload = finalResult
            };
        }
    }
}
