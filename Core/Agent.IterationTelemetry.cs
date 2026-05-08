using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private void LogExecutionProfileIfNeeded(bool unrestrictedSandboxMode, ExecutionTracer tracer)
        {
            if (!unrestrictedSandboxMode)
            {
                return;
            }

            _memory.Add("execution_profile", "UNRESTRICTED_INSIDE_SANDBOX");
            tracer.LogActionEvent("ExecutionProfile", "Agent", ExecutionTracer.ActionLogLevel.Warning, "unrestricted_inside_sandbox", metadata: new Dictionary<string, object?>
            {
                { "access_mode", _sessionContext?.AccessMode.ToString() ?? string.Empty },
                { "env_flag", "LOCALCURSOR_UNRESTRICTED_SANDBOX" }
            });
        }

        private void LogIterationLoopStarted(ExecutionTracer tracer)
        {
            tracer.LogActionEvent("IterationLoopStarted", "AgentIterationLoop", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
            {
                { "max_iterations", MAX_ITERATIONS },
                { "loop_stage", "AgentIterationLoop" }
            });
        }

        private void LogIterationStarted(ExecutionTracer tracer, int actualIterationsUsed)
        {
            tracer.LogActionEvent("IterationStarted", "AgentIterationLoop", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
            {
                { "iteration", actualIterationsUsed },
                { "max_iterations", MAX_ITERATIONS }
            });
        }

        private void LogIterationCompleted(
            ExecutionTracer tracer,
            int actualIterationsUsed,
            string lastSuccessfulStep,
            string lastKnownAction)
        {
            tracer.LogActionEvent("IterationCompleted", "AgentIterationLoop", ExecutionTracer.ActionLogLevel.Info, "completed", metadata: new Dictionary<string, object?>
            {
                { "iteration", actualIterationsUsed },
                { "max_iterations", MAX_ITERATIONS },
                { "last_successful_step", lastSuccessfulStep },
                { "last_known_action", lastKnownAction }
            });
        }
    }
}
