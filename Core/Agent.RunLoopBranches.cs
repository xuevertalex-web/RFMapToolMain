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

        private bool TryValidateMutationToolCalls(
            string task,
            List<ToolCaller.ToolCall> toolCalls,
            ToolCaller.ToolCall mutationCall,
            TargetResolutionGateResult targetResolution,
            ExecutionTracer tracer,
            out string finalResult)
        {
            var intentGate = new IntentConfirmationGate(_contextBuilder.Tracer);
            var intentDecision = intentGate.Evaluate(task, mutationCall.Input, targetResolution);
            _memory.Add("intent_confirmation_gate", $"{intentDecision.ReasonCode}:{intentDecision.ClassifiedKind}:{intentDecision.Outcome}");

            if (intentDecision.IsRejected)
            {
                var safeFailure = intentDecision.Reason;
                _memory.Add("context_failure", safeFailure, intentDecision.ReasonCode);
                tracer.MarkStopPoint("IntentConfirmationGate", intentDecision.ReasonCode, safeFailure, new[] { "MultiFileGate", "PatchApply", "BuildVerification" });
                finalResult = FinalizeRunResult(
                    false,
                    safeFailure,
                    $"Intent confirmation gate failed: {intentDecision.ReasonCode}",
                    intentDecision.ReasonCode,
                    Array.Empty<string>(),
                    Array.Empty<ChangedHint>(),
                    Array.Empty<ChangedRange>(),
                    Array.Empty<ChangedKind>(),
                    false);
                return true;
            }

            var multiFileGate = new MultiFileEditGate(_contextBuilder.Tracer);
            var multiFileDecision = multiFileGate.Evaluate(task, toolCalls, targetResolution, intentDecision);
            _memory.Add("multi_file_edit_gate", $"{multiFileDecision.ReasonCode}:{multiFileDecision.ClassifiedKind}:{multiFileDecision.Outcome}");

            if (multiFileDecision.IsRejected)
            {
                var safeFailure = multiFileDecision.Reason;
                _memory.Add("context_failure", safeFailure, multiFileDecision.ReasonCode);
                tracer.MarkStopPoint("MultiFileEditGate", multiFileDecision.ReasonCode, safeFailure, new[] { "PatchApply", "BuildVerification" });
                finalResult = FinalizeRunResult(
                    false,
                    safeFailure,
                    $"Multi-file edit gate failed: {multiFileDecision.ReasonCode}",
                    multiFileDecision.ReasonCode,
                    Array.Empty<string>(),
                    Array.Empty<ChangedHint>(),
                    Array.Empty<ChangedRange>(),
                    Array.Empty<ChangedKind>(),
                    false);
                return true;
            }

            finalResult = string.Empty;
            return false;
        }

        private async Task RecordWriteToolEffectsAsync(
            string task,
            List<ToolCaller.ToolCall> toolCalls,
            List<string> resolvedFiles,
            HashSet<string> changedFiles,
            Dictionary<string, ChangedHint> changedHints,
            Dictionary<string, ChangedRange> changedRanges,
            Dictionary<string, ChangedKind> changedKinds,
            ExecutionTracer tracer)
        {
            foreach (var call in toolCalls)
            {
                if (!call.ToolName.Equals("file", StringComparison.OrdinalIgnoreCase) ||
                    !call.Input.StartsWith("write:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var filePath = WriteTargetPathExtractor.ExtractWriteTargetPath(call.Input);
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    continue;
                }

                changedFiles.Add(filePath);
                tracer.MarkChangedFile(filePath);
                var patchDecision = PatchDecisionBuilder.BuildPatchDecision(filePath, call.Input, resolvedFiles);
                _contextBuilder.Tracer.LogPatchDecision(patchDecision);
                changedHints[filePath] = ChangedHintComposer.BuildChangedHint(filePath, call.Input, patchDecision);
                var changedRange = ChangedRangeResolver.BuildChangedRange(filePath, call.Input, patchDecision, _projectIndexer.SymbolDirectory);
                if (changedRange != null)
                {
                    changedRanges[filePath] = changedRange;
                }

                changedKinds[filePath] = ChangedKindBuilder.BuildChangedKind(task, call.Input, patchDecision, buildResult: null);
                _fileStateManager.MarkHot(filePath);
                await _projectIndexer.ReindexFile(filePath);
            }
        }

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
