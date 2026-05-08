namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private LoopDecision HandleEmptyParsedToolCalls(string task, bool analysisOnlyTask, string currentResponse)
        {
            if (NoToolResponseHeuristics.IsNonSubstantiveNoToolResponse(currentResponse))
            {
                return LoopDecision.Continue("Your previous response did not contain the final analysis. Provide the final answer now. Do not say what you will do. Do not ask for more steps. Do not emit a tool call.");
            }

            if (!analysisOnlyTask && (TaskIntentClassifier.IsBroadEngineeringIntent(task) || TaskIntentClassifier.IsTechnicalAnalysisIntent(task)))
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

            _memory.Add("final_response", currentResponse);
            return LoopDecision.Finalize(FinalizeRunResult(
                true,
                string.IsNullOrWhiteSpace(currentResponse) ? "Agent run completed successfully." : currentResponse,
                analysisOnlyTask ? "Analysis response generated" : "Agent completed without tool calls",
                analysisOnlyTask ? "SUCCESS_ANALYSIS_RESPONSE" : "SUCCESS_NO_TOOL_CALLS",
                Array.Empty<string>(),
                Array.Empty<ChangedHint>(),
                Array.Empty<ChangedRange>(),
                Array.Empty<ChangedKind>(),
                false));
        }
    }
}
