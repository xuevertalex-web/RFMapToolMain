using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private string BuildTerminalFailureResult(ExecutionTracer tracer, AgentRunState runState)
        {
            return FinalizeMaxIterationsFailure(
                tracer,
                runState.ActualIterationsUsed,
                runState.LastSuccessfulStep,
                runState.LastKnownAction,
                runState.ModelCallStarted,
                runState.PatchStarted,
                runState.BuildStarted,
                runState.LastBuildFailureCode,
                runState.LastBuildExitCode,
                runState.LastBuildTimedOut,
                runState.LastBuildErrorMessageTruncated,
                runState.LastBuildErrorMessageLength);
        }
    }
}
