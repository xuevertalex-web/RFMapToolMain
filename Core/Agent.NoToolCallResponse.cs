namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private LoopDecision HandleNoToolCallResponse(string task, string currentResponse, string? requestedNewFile)
        {
            if (MutationIntentDetector.IsMutationIntentTask(task) || requestedNewFile != null)
            {
                return LoopDecision.Continue(
                    requestedNewFile != null
                        ? $"This is a file creation task. Use the file tool now to write:{requestedNewFile}:... and create the requested file. Do not answer with explanation only."
                        : "This is a code change task. Use the file tool now to write Program.cs and make a concrete edit. Do not answer with code only.");
            }

            if (NoToolResponseHeuristics.IsNonSubstantiveNoToolResponse(currentResponse))
            {
                return LoopDecision.Continue("Your previous response did not contain the final analysis. Provide the final answer now. Do not say what you will do. Do not ask for more steps. Do not emit a tool call.");
            }

            _memory.Add("final_response", currentResponse);
            if (TaskIntentClassifier.IsBroadEngineeringIntent(task) || TaskIntentClassifier.IsTechnicalAnalysisIntent(task))
            {
                _memory.Add("task_status", "needs_action_plan");
                return LoopDecision.Finalize(FinalizeRunResult(
                    false,
                    "The task requires an actionable engineering plan or concrete edits, but no tool/action step was produced.",
                    "No actionable steps produced for broad engineering intent",
                    "NO_ACTIONABLE_STEPS",
                    Array.Empty<string>(),
                    Array.Empty<ChangedHint>(),
                    Array.Empty<ChangedRange>(),
                    Array.Empty<ChangedKind>(),
                    false));
            }

            return LoopDecision.Finalize(FinalizeRunResult(
                true,
                string.IsNullOrWhiteSpace(currentResponse) ? "Agent run completed successfully." : currentResponse,
                "Agent completed without tool calls",
                "SUCCESS_NO_TOOL_CALLS",
                Array.Empty<string>(),
                Array.Empty<ChangedHint>(),
                Array.Empty<ChangedRange>(),
                Array.Empty<ChangedKind>(),
                false));
        }
    }
}
