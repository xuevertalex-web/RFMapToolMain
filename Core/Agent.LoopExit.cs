using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private static string BuildPostToolContinuationResponse(
            bool analysisOnlyTask,
            bool mutationIntentTask,
            ToolCaller.ToolCall? mutationCall,
            int changedFileCount,
            string? requestedNewFile,
            string currentResponse)
        {
            if (analysisOnlyTask && mutationCall == null)
            {
                return "You have enough context. Provide a final answer now with no tool call.";
            }

            if (mutationIntentTask && mutationCall == null && changedFileCount == 0)
            {
                return requestedNewFile != null
                    ? $"This is a new-file creation task. Use the file tool now with write:{requestedNewFile}:... and create the requested file directly. Do not ask for clarification and do not answer without a tool call."
                    : "This is a code change task. Make one concrete code edit now using the file tool. Do not keep reading or building without writing a change.";
            }

            return currentResponse + MutationContinuationPrompt.ContinueIfNotComplete;
        }

        private string FinalizeMaxIterationsFailure(
            ExecutionTracer tracer,
            int actualIterationsUsed,
            string lastSuccessfulStep,
            string lastKnownAction,
            bool modelCallStarted,
            bool patchStarted,
            bool buildStarted,
            string? lastBuildFailureCode,
            int? lastBuildExitCode,
            bool? lastBuildTimedOut,
            bool? lastBuildErrorMessageTruncated,
            int? lastBuildErrorMessageLength)
        {
            var finalMessage = MaxIterationsFailurePayloadFactory.FailureMessage;
            _memory.Add(MaxIterationsFailurePayloadFactory.TaskStatusKey, MaxIterationsFailurePayloadFactory.TaskStatusValue);
            tracer.LogActionEvent("MaxIterationsReached", MaxIterationsFailurePayloadFactory.FailureStage, ExecutionTracer.ActionLogLevel.Warning, "failed", MaxIterationsFailurePayloadFactory.FailureCode, new Dictionary<string, object?>
            {
                { "loop_stage", MaxIterationsFailurePayloadFactory.FailureStage },
                { "max_iterations", MAX_ITERATIONS },
                { "iterations_used", actualIterationsUsed },
                { "last_successful_step", lastSuccessfulStep },
                { "failed_step", MaxIterationsFailurePayloadFactory.FailureStep },
                { "last_known_action", lastKnownAction },
                { "model_call_started", modelCallStarted },
                { "patch_started", patchStarted },
                { "build_started", buildStarted },
                { "pipeline_stopped_reason", MaxIterationsFailurePayloadFactory.PipelineStoppedReason }
            });
            tracer.MarkStopPoint(MaxIterationsFailurePayloadFactory.FailureStage, MaxIterationsFailurePayloadFactory.FailureCode, finalMessage, MaxIterationsFailurePayloadFactory.BuildDownstreamNotStarted(buildStarted));
            tracer.LogActionEvent("RunFailedWithRootCause", "Agent", ExecutionTracer.ActionLogLevel.Warning, "failed", MaxIterationsFailurePayloadFactory.FailureCode, new Dictionary<string, object?>
            {
                { "root_cause_code", MaxIterationsFailurePayloadFactory.FailureCode },
                { "failed_stage", MaxIterationsFailurePayloadFactory.FailureStage },
                { "failed_step", MaxIterationsFailurePayloadFactory.FailureStep }
            });

            return FinalizeRunResult(
                false,
                finalMessage,
                "Maximum iterations reached",
                MaxIterationsFailurePayloadFactory.FailureCode,
                Array.Empty<string>(),
                Array.Empty<ChangedHint>(),
                Array.Empty<ChangedRange>(),
                Array.Empty<ChangedKind>(),
                false,
                buildStarted: buildStarted,
                failure: MaxIterationsFailurePayloadFactory.Create(
                    buildStarted,
                    lastSuccessfulStep,
                    lastKnownAction,
                    actualIterationsUsed,
                    MAX_ITERATIONS,
                    modelCallStarted,
                    patchStarted,
                    lastBuildFailureCode ?? string.Empty,
                    lastBuildExitCode,
                    lastBuildTimedOut,
                    lastBuildErrorMessageTruncated,
                    lastBuildErrorMessageLength));
        }
    }
}
