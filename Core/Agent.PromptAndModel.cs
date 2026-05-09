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

            const int maxAttempts = 3;
            var retryCount = 0;
            LlmRuntimeResult? runtimeResult = null;
            string response = string.Empty;
            string errorType = string.Empty;
            var modelStart = DateTime.UtcNow;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    runtimeResult = runtimeClient is null
                        ? null
                        : await runtimeClient.GenerateNormalized(prompt, CancellationToken.None);
                    response = runtimeResult?.Completion ?? await _llmClient.Generate(prompt, CancellationToken.None);
                    errorType = MapRuntimeStatusToErrorType(runtimeResult?.Status);
                    break;
                }
                catch (Exception ex)
                {
                    errorType = ClassifyProviderError(ex.Message);
                    if (attempt >= maxAttempts)
                    {
                        _lastLlmRetryCount = retryCount;
                        _lastLlmErrorType = errorType;
                        throw;
                    }

                    retryCount++;
                    tracer.LogActionEvent("ModelRequestRetry", "Agent", ExecutionTracer.ActionLogLevel.Warning, "retrying", errorType, metadata: new Dictionary<string, object?>
                    {
                        { "attempt", attempt },
                        { "max_attempts", maxAttempts },
                        { "error", ex.Message }
                    });
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), CancellationToken.None);
                }
            }
            _lastLlmRetryCount = retryCount;
            _lastLlmErrorType = errorType;
            tracer.LogActionEvent("ModelRequest", "Agent", ExecutionTracer.ActionLogLevel.Info, "completed", metadata: new Dictionary<string, object?>
            {
                { "operation_kind", "task_iteration" },
                { "response_preview", response.Length > 200 ? response[..200] : response },
                { "iteration", iteration },
                { "retry_count", retryCount },
                { "error_type", errorType }
            }, durationMs: (long)(DateTime.UtcNow - modelStart).TotalMilliseconds);
            _memory.Add("llm_response", response.Length > 100 ? response.Substring(0, 100) + "..." : response);

            return new ModelRequestResult
            {
                Response = response,
                RuntimeResult = runtimeResult
            };
        }

        private static string MapRuntimeStatusToErrorType(LlmRuntimeStatus? status) => status switch
        {
            LlmRuntimeStatus.ModelTimeout => "provider_timeout",
            LlmRuntimeStatus.ProviderUnavailable => "provider_unavailable",
            LlmRuntimeStatus.ModelOutputEmpty or LlmRuntimeStatus.ModelOutputParseFailed => "invalid_response",
            LlmRuntimeStatus.LlmRequestFailed => "provider_unavailable",
            _ => string.Empty
        };

        private static string ClassifyProviderError(string message)
        {
            var text = message ?? string.Empty;
            if (text.Contains("429", StringComparison.OrdinalIgnoreCase) || text.Contains("rate", StringComparison.OrdinalIgnoreCase))
                return "provider_rate_limit";
            if (text.Contains("timeout", StringComparison.OrdinalIgnoreCase) || text.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                return "provider_timeout";
            if (text.Contains("unavailable", StringComparison.OrdinalIgnoreCase) || text.Contains("connection", StringComparison.OrdinalIgnoreCase) || text.Contains("refused", StringComparison.OrdinalIgnoreCase))
                return "provider_unavailable";
            return "invalid_response";
        }
    }
}
