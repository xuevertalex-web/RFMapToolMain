using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.LLM.Runtime;
using LocalCursorAgent.Memory;
using System.Linq;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class ModelRequestResult
        {
            public string Response { get; init; } = string.Empty;
            public LlmRuntimeResult? RuntimeResult { get; init; }
        }

        private (string promptKind, string prompt) BuildIterationPrompt(
            string task,
            bool analysisOnlyTask,
            int iteration,
            string currentResponse,
            string contextString,
            ExecutionTracer tracer)
        {
            var regressionAdvice = _regressionAdvisor?.BuildAdvice(task) ?? RegressionAdvice.Empty;
            if (regressionAdvice.HasAdvice)
            {
                tracer.LogActionEvent("RegressionAdvice", "Agent", ExecutionTracer.ActionLogLevel.Info, "selected", metadata: new Dictionary<string, object?>
                {
                    { "record_count", regressionAdvice.Records.Count },
                    { "outcome_classes", regressionAdvice.Records.Select(x => x.OutcomeClass).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() },
                    { "prompt_shaping", regressionAdvice.PromptShapingText },
                    { "strategy_bias", regressionAdvice.StrategyBiasText }
                });
            }

            var promptKind = analysisOnlyTask ? "BuildAnalysisPromptWithContext" : "BuildPromptWithContext";
            var prompt = analysisOnlyTask
                ? AnalysisPromptBuilder.BuildAnalysisPromptWithContext(task, iteration, currentResponse, contextString, ResponseLanguageHelper.BuildResponseLanguageRule(task))
                : BuildPromptWithContext(task, iteration, currentResponse, contextString, regressionAdvice.Text, regressionAdvice.PromptShapingText, regressionAdvice.StrategyBiasText);
            _memory.Add("prompt_sent", prompt.Length > 100 ? prompt.Substring(0, 100) + "..." : prompt);
            return (promptKind, prompt);
        }

        private async Task<ModelRequestResult> ExecuteModelRequestAsync(
            string prompt,
            string promptKind,
            int iteration,
            ILlmRuntimeClient? runtimeClient,
            ExecutionTracer tracer)
        {
            tracer.LogActionEvent("ModelCallStarted", "Agent", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
            {
                { "operation_kind", "task_iteration" },
                { "prompt_kind", promptKind },
                { "iteration", iteration }
            });
            tracer.LogActionEvent("ModelRequest", "Agent", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
            {
                { "operation_kind", "task_iteration" },
                { "prompt_kind", promptKind },
                { "prompt_preview", prompt.Length > 200 ? prompt[..200] : prompt },
                { "iteration", iteration }
            });

            var modelStart = DateTime.UtcNow;
            var runtimeResult = runtimeClient is null
                ? null
                : await runtimeClient.GenerateNormalized(prompt, CancellationToken.None);
            var response = runtimeResult?.Completion ?? await _llmClient.Generate(prompt, CancellationToken.None);
            tracer.LogActionEvent("ModelRequest", "Agent", ExecutionTracer.ActionLogLevel.Info, "completed", metadata: new Dictionary<string, object?>
            {
                { "operation_kind", "task_iteration" },
                { "response_preview", response.Length > 200 ? response[..200] : response },
                { "iteration", iteration }
            }, durationMs: (long)(DateTime.UtcNow - modelStart).TotalMilliseconds);
            _memory.Add("llm_response", response.Length > 100 ? response.Substring(0, 100) + "..." : response);

            return new ModelRequestResult
            {
                Response = response,
                RuntimeResult = runtimeResult
            };
        }
    }
}
