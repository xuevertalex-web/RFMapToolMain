using LocalCursorAgent.Context;
using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.LLM.Runtime;
using System.Linq;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private bool TryRejectTaskBeforeExecution(string task, ExecutionTracer tracer, out string finalResult)
        {
            var intent = TaskIntentScorer.Classify(task);
            var analysisOnlyTask = TaskPrecheckHeuristics.IsAnalysisOnlyTask(task);
            if (!analysisOnlyTask && intent == TaskIntentKind.Chat)
            {
                var message = BuildChatResponse(task);
                tracer.MarkStopPoint("Agent", "SUCCESS_NO_TOOL_CALLS", "Conversational response without execution", new[] { "Indexing", "ModelRequest", "PatchApply", "BuildVerification" });
                finalResult = FinalizeRunResult(
                    true,
                    message,
                    "Conversational response generated",
                    "SUCCESS_NO_TOOL_CALLS",
                    Array.Empty<string>(),
                    Array.Empty<ChangedHint>(),
                    Array.Empty<ChangedRange>(),
                    Array.Empty<ChangedKind>(),
                    false,
                    runStartedUtc: DateTime.UtcNow,
                    workspace: _sessionContext?.ActiveWorkspaceRoot,
                    payloadFinalStatus: "success");
                return true;
            }

            if (!analysisOnlyTask && intent == TaskIntentKind.Clarify)
            {
                var message = "Уточни, что именно нужно сделать: создать файл, изменить код или проверить ошибку? Напиши цель и файл/путь, если он известен.";
                tracer.MarkStopPoint("Agent", "CLARIFICATION_REQUIRED", message, new[] { "Indexing", "ModelRequest", "PatchApply", "BuildVerification" });
                finalResult = FinalizeRunResult(
                    false,
                    message,
                    "Clarification required before execution",
                    "CLARIFICATION_REQUIRED",
                    Array.Empty<string>(),
                    Array.Empty<ChangedHint>(),
                    Array.Empty<ChangedRange>(),
                    Array.Empty<ChangedKind>(),
                    false,
                    runStartedUtc: DateTime.UtcNow,
                    workspace: _sessionContext?.ActiveWorkspaceRoot,
                    payloadFinalStatus: "clarification-required");
                return true;
            }

            if (TaskPrecheckHeuristics.IsSuspiciousInjectedToolTask(task))
            {
                var message = "Task contains raw tool syntax. Provide a normal natural-language task instead.";
                tracer.MarkStopPoint("Agent", "TASK_CONTAINS_TOOL_SYNTAX", message, new[] { "Indexing", "ModelRequest", "PatchApply", "BuildVerification" });
                finalResult = FinalizeRunResult(false, message, "Task rejected before execution", "TASK_CONTAINS_TOOL_SYNTAX", Array.Empty<string>(), Array.Empty<ChangedHint>(), Array.Empty<ChangedRange>(), Array.Empty<ChangedKind>(), false);
                return true;
            }

            if (TaskPrecheckHeuristics.IsLowSignalTask(task))
            {
                var message = "Task is too short or ambiguous. Provide a concrete natural-language request.";
                tracer.MarkStopPoint("Agent", "NON_ACTIONABLE_TASK", message, new[] { "Indexing", "ModelRequest", "PatchApply", "BuildVerification" });
                finalResult = FinalizeRunResult(false, message, "Task rejected before execution", "NON_ACTIONABLE_TASK", Array.Empty<string>(), Array.Empty<ChangedHint>(), Array.Empty<ChangedRange>(), Array.Empty<ChangedKind>(), false);
                return true;
            }

            finalResult = string.Empty;
            return false;
        }

        private static string BuildChatResponse(string task)
        {
            var value = (task ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Contains("что ты умеешь") || value.Contains("что умеешь"))
                return "Я могу: объяснить проект и код, предложить план, а по явной задаче — изменить файлы и проверить результат.";
            if (value.Contains("объясни") || value.Contains("опиши проект"))
                return "Это локальный coding-агент: он анализирует проект, выполняет явные инженерные задачи и возвращает structured результат с проверками.";
            return "Я на связи. Сформулируй задачу: что создать, изменить или проверить.";
        }

        private bool TryHandleHardModelFailure(
            bool analysisOnlyTask,
            LlmRuntimeResult? runtimeResult,
            ContextInformation contextInfo,
            int iteration,
            string currentResponse,
            HashSet<string> changedFiles,
            Dictionary<string, ChangedHint> changedHints,
            Dictionary<string, ChangedRange> changedRanges,
            Dictionary<string, ChangedKind> changedKinds,
            DateTime runStartedUtc,
            LlmProviderMetadata? runtimeMetadata,
            out string finalResult)
        {
            _memory.Add("llm_failure", currentResponse, "LlmUnavailableOrRequestFailed");
            if (analysisOnlyTask)
            {
                var fallbackReason = FallbackReasonResolver.Resolve(runtimeResult, currentResponse, TimeoutResponseHeuristics.IsModelTimeoutResponse);
                if (string.Equals(fallbackReason, "MODEL_TIMEOUT", StringComparison.Ordinal))
                {
                    _contextBuilder.Tracer.LogActionEvent("ModelCallTimedOut", "Agent", ExecutionTracer.ActionLogLevel.Warning, "timed_out", fallbackReason, metadata: new Dictionary<string, object?>
                    {
                        { "iteration", iteration }
                    });
                }

                _contextBuilder.Tracer.LogActionEvent("AnalysisFallbackStarted", "Agent", ExecutionTracer.ActionLogLevel.Warning, "started", fallbackReason, metadata: new Dictionary<string, object?>
                {
                    { "fallback_mode", "INDEXED_CONTEXT_SUMMARY" },
                    { "provider_outcome", runtimeResult?.Status.ToString() ?? "legacy_classifier" },
                    { "response_length", currentResponse.Length },
                    { "response_preview", string.IsNullOrWhiteSpace(currentResponse) ? string.Empty : (currentResponse.Length > 120 ? currentResponse[..120] : currentResponse) }
                });

                var fallbackSummary = AnalysisFallbackFormatter.BuildAnalysisFallbackSummary(contextInfo, fallbackReason);
                _contextBuilder.Tracer.LogActionEvent("AnalysisFallback", "Agent", ExecutionTracer.ActionLogLevel.Warning, "used", fallbackReason, metadata: new Dictionary<string, object?>
                {
                    { "selected_files", contextInfo.SelectedFiles.ToArray() },
                    { "file_count", contextInfo.SelectedFiles.Count }
                });
                _contextBuilder.Tracer.LogActionEvent("AnalysisFallbackCompleted", "Agent", ExecutionTracer.ActionLogLevel.Info, "completed", fallbackReason, metadata: new Dictionary<string, object?>
                {
                    { "fallback_mode", "INDEXED_CONTEXT_SUMMARY" },
                    { "file_count", contextInfo.SelectedFiles.Count }
                });

                finalResult = FinalizeRunResult(
                    true,
                    fallbackSummary,
                    "Analysis summary generated from indexed project context",
                    "ANALYSIS_FALLBACK_USED",
                    Array.Empty<string>(),
                    Array.Empty<ChangedHint>(),
                    Array.Empty<ChangedRange>(),
                    Array.Empty<ChangedKind>(),
                    false,
                    runStartedUtc: runStartedUtc,
                    workspace: _sessionContext?.ActiveWorkspaceRoot,
                    provider: LlmClientIdentityResolver.ResolveProviderName(_llmClient, runtimeMetadata),
                    model: LlmClientIdentityResolver.ResolveModelName(_llmClient, runtimeMetadata),
                    degradedFlags: new[] { "ANALYSIS_FALLBACK_USED" },
                    fallbackReason: fallbackReason,
                    fallbackMode: "INDEXED_CONTEXT_SUMMARY",
                    payloadFinalStatus: "fallback-success",
                    timeline: TimelineBuilder.BuildAnalysisTimeline(modelTimedOut: string.Equals(fallbackReason, "MODEL_TIMEOUT", StringComparison.Ordinal), fallbackUsed: true));
                return true;
            }

            finalResult = FinalizeStructuredDiagnosticResult(
                "LLM_REQUEST_FAILED",
                new StructuredDiagnostic
                {
                    RootCause = "The configured LLM provider failed before the agent could plan or execute tool calls.",
                    AttemptedFix = "Requested a completion from the active LLM provider.",
                    WhyDenied = currentResponse,
                    NextSafeAction = "Start the configured LLM service, switch to an available provider, or configure a fallback provider and retry the same task."
                },
                changedFiles,
                changedHints.Values,
                changedRanges.Values,
                changedKinds.Values);
            return true;
        }

        private bool TryHandleAnalysisDirectResponse(
            string task,
            bool analysisOnlyTask,
            string currentResponse,
            ContextInformation contextInfo,
            DateTime runStartedUtc,
            LlmProviderMetadata? runtimeMetadata,
            out string finalResult)
        {
            if (!analysisOnlyTask ||
                string.IsNullOrWhiteSpace(currentResponse) ||
                currentResponse.TrimStart().StartsWith("TOOL:", StringComparison.OrdinalIgnoreCase))
            {
                finalResult = string.Empty;
                return false;
            }

            if (TaskIntentClassifier.IsTechnicalAnalysisIntent(task) &&
                (contextInfo.SelectedFiles.Count == 0 || NoToolResponseHeuristics.IsNonSubstantiveNoToolResponse(currentResponse) || NoToolResponseHeuristics.IsNeedsMoreDataResponse(currentResponse)))
            {
                _memory.Add("task_status", "needs_action_plan");
                finalResult = FinalizeRunResult(
                    false,
                    "Technical analysis request did not produce grounded workspace analysis. Produce actionable target/context steps first.",
                    "No actionable steps produced for technical analysis intent",
                    "NO_ACTIONABLE_STEPS",
                    Array.Empty<string>(),
                    Array.Empty<ChangedHint>(),
                    Array.Empty<ChangedRange>(),
                    Array.Empty<ChangedKind>(),
                    false);
                return true;
            }

            _memory.Add("final_response", currentResponse);
            finalResult = FinalizeRunResult(
                true,
                currentResponse,
                "Analysis response generated",
                "SUCCESS_ANALYSIS_RESPONSE",
                Array.Empty<string>(),
                Array.Empty<ChangedHint>(),
                Array.Empty<ChangedRange>(),
                Array.Empty<ChangedKind>(),
                false,
                runStartedUtc: runStartedUtc,
                workspace: _sessionContext?.ActiveWorkspaceRoot,
                provider: LlmClientIdentityResolver.ResolveProviderName(_llmClient, runtimeMetadata),
                model: LlmClientIdentityResolver.ResolveModelName(_llmClient, runtimeMetadata),
                degradedFlags: Array.Empty<string>(),
                fallbackReason: string.Empty,
                fallbackMode: string.Empty,
                payloadFinalStatus: "success",
                timeline: TimelineBuilder.BuildAnalysisTimeline(modelTimedOut: false, fallbackUsed: false));
            return true;
        }

    }
}
