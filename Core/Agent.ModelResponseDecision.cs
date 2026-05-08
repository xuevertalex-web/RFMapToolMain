using LocalCursorAgent.Context;
using LocalCursorAgent.LLM.Runtime;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class ModelResponseDecisionResult
        {
            public required bool IsHandled { get; init; }
            public string? FinalResult { get; init; }
        }

        private ModelResponseDecisionResult HandleModelResponseDecision(
            string task,
            bool analysisOnlyTask,
            LlmRuntimeResult? runtimeResult,
            int iteration,
            string currentResponse,
            ContextInformation contextInfo,
            DateTime runStartedUtc,
            LlmProviderMetadata? runtimeMetadata,
            HashSet<string> changedFiles,
            Dictionary<string, ChangedHint> changedHints,
            Dictionary<string, ChangedRange> changedRanges,
            Dictionary<string, ChangedKind> changedKinds)
        {
            var isHardFailure = runtimeResult?.IsFailure ?? LlmFailureDetector.IsHardLlmFailureResponse(currentResponse);
            if (isHardFailure &&
                TryHandleHardModelFailure(
                    analysisOnlyTask,
                    runtimeResult,
                    contextInfo,
                    iteration,
                    currentResponse,
                    changedFiles,
                    changedHints,
                    changedRanges,
                    changedKinds,
                    runStartedUtc,
                    runtimeMetadata,
                    out var hardFailureResult))
            {
                return new ModelResponseDecisionResult
                {
                    IsHandled = true,
                    FinalResult = hardFailureResult
                };
            }

            if (TryHandleAnalysisDirectResponse(task, analysisOnlyTask, currentResponse, contextInfo, runStartedUtc, runtimeMetadata, out var analysisResult))
            {
                return new ModelResponseDecisionResult
                {
                    IsHandled = true,
                    FinalResult = analysisResult
                };
            }

            return new ModelResponseDecisionResult
            {
                IsHandled = false
            };
        }
    }
}
