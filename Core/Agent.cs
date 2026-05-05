using LocalCursorAgent.LLM;
using LocalCursorAgent.Memory;
using LocalCursorAgent.Execution;
using LocalCursorAgent.Tools;
using LocalCursorAgent.Indexing;
using LocalCursorAgent.Context;
using LocalCursorAgent.Embeddings;
using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Security;
using LocalCursorAgent.LLM.Runtime;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalCursorAgent.Core
{
#pragma warning disable CS0162
    /// <summary>
    /// Main AI coding agent that orchestrates the tool-calling loop with semantic understanding.
    /// Integrates file state awareness for active context layer.
    /// </summary>
    public class Agent
    {
        private readonly ILLMClient _llmClient;
        private readonly ToolCaller _toolCaller;
        private readonly ToolRegistry _toolRegistry;
        private readonly MemoryStore _memory;
        private readonly BuildVerifier _buildVerifier;
        private readonly SandboxManager _sandboxManager;
        private readonly ProjectIndexer _projectIndexer;
        private readonly ContextBuilder _contextBuilder;
        private readonly FileStateManager _fileStateManager;
        private readonly AgentMemorySystem _memorySystem;
        private readonly RunRegressionAdvisor? _regressionAdvisor;
        private readonly AgentSessionContext? _sessionContext;
        private readonly WorkspaceResolutionResult? _workspaceResolution;

        private const int MAX_ITERATIONS = 3;
        private const int CONTEXT_WINDOW = 15;
        private const int CONTEXT_EXPANSION_BUFFER = 5;
        private const bool VERBOSE_OUTPUT = false;

        public Agent(
            ILLMClient llmClient,
            ToolRegistry toolRegistry,
            MemoryStore memory,
            BuildVerifier buildVerifier,
            SandboxManager sandboxManager,
            ProjectIndexer projectIndexer,
            ContextBuilder contextBuilder,
            FileStateManager? fileStateManager = null,
            AgentSessionContext? sessionContext = null,
            WorkspaceResolutionResult? workspaceResolution = null)
        {
            _llmClient = llmClient;
            _toolRegistry = toolRegistry;
            _memory = memory;
            _buildVerifier = buildVerifier;
            _sandboxManager = sandboxManager;
            _projectIndexer = projectIndexer;
            _contextBuilder = contextBuilder;
            _fileStateManager = fileStateManager ?? new FileStateManager();
            _memorySystem = new AgentMemorySystem();
            _regressionAdvisor = !string.IsNullOrWhiteSpace(sessionContext?.RuntimeRoot)
                ? new RunRegressionAdvisor(sessionContext.RuntimeRoot)
                : null;
            _sessionContext = sessionContext;
            _workspaceResolution = workspaceResolution;
            _toolCaller = new ToolCaller(toolRegistry);
        }

        /// <summary>
        /// Run the agent loop for a given task.
        /// </summary>
        public async Task<string> RunTask(string task)
        {
            var runStartedUtc = DateTime.UtcNow;
            _memory.Add("task_start", task);
            var tracer = _contextBuilder.Tracer;
            var requestedNewFile = NewFilePathExtractor.ExtractRequestedNewFilePath(task);
            tracer.LogActionEvent("TaskReceived", "Agent", ExecutionTracer.ActionLogLevel.Info, "received", metadata: new Dictionary<string, object?>
            {
                { "task", task }
            });
            tracer.LogActionEvent("TaskLifecycle", "Agent", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
            {
                { "task", task },
                { "requested_new_file", requestedNewFile ?? string.Empty }
            });

            if (TaskPrecheckHeuristics.IsSuspiciousInjectedToolTask(task))
            {
                var message = "Task contains raw tool syntax. Provide a normal natural-language task instead.";
                tracer.MarkStopPoint("Agent", "TASK_CONTAINS_TOOL_SYNTAX", message, new[] { "Indexing", "ModelRequest", "PatchApply", "BuildVerification" });
                return FinalizeRunResult(false, message, "Task rejected before execution", "TASK_CONTAINS_TOOL_SYNTAX", Array.Empty<string>(), Array.Empty<ChangedHint>(), Array.Empty<ChangedRange>(), Array.Empty<ChangedKind>(), false);
            }

            if (TaskPrecheckHeuristics.IsLowSignalTask(task))
            {
                var message = "Task is too short or ambiguous. Provide a concrete natural-language request.";
                tracer.MarkStopPoint("Agent", "NON_ACTIONABLE_TASK", message, new[] { "Indexing", "ModelRequest", "PatchApply", "BuildVerification" });
                return FinalizeRunResult(false, message, "Task rejected before execution", "NON_ACTIONABLE_TASK", Array.Empty<string>(), Array.Empty<ChangedHint>(), Array.Empty<ChangedRange>(), Array.Empty<ChangedKind>(), false);
            }

            try
            {
                // Index project for semantic search
                tracer.LogActionEvent("IndexingStarted", "Agent", ExecutionTracer.ActionLogLevel.Info, "started");
                tracer.LogActionEvent("Indexing", "Agent", ExecutionTracer.ActionLogLevel.Info, "started");
                var indexResult = await _projectIndexer.IndexProject();
                tracer.UpdateRunIndexingStatus(indexResult.Success ? "completed" : "failed");
                tracer.LogActionEvent("IndexingCompleted", "Agent", indexResult.Success ? ExecutionTracer.ActionLogLevel.Info : ExecutionTracer.ActionLogLevel.Warning, indexResult.Success ? "completed" : "failed", indexResult.Success ? null : "INDEXING_FAILED", new Dictionary<string, object?>
                {
                    { "files_processed", indexResult.FilesProcessed },
                    { "error", indexResult.Error ?? string.Empty }
                });
                tracer.LogActionEvent("Indexing", "Agent", indexResult.Success ? ExecutionTracer.ActionLogLevel.Info : ExecutionTracer.ActionLogLevel.Warning, indexResult.Success ? "completed" : "failed", indexResult.Success ? null : "INDEXING_FAILED", new Dictionary<string, object?>
                {
                    { "files_processed", indexResult.FilesProcessed },
                    { "error", indexResult.Error ?? string.Empty }
                });
                
                if (indexResult.Success)
                {
                    _memory.Add("indexing_complete", $"Indexed {indexResult.FilesProcessed} files");
                }

                var targetResolutionGate = new TargetResolutionGate(_projectIndexer, _contextBuilder.Tracer);
                var targetResolution = await targetResolutionGate.ResolveAsync(task);
                
                // Если целевое разрешение не удалось - просто продолжаем без ограничений, агент сам решит куда писать
                if (targetResolution.IsFailed)
                {
                    _memory.Add("target_resolution_gate", $"SKIPPED:{targetResolution.ReasonCode}:{targetResolution.Reason}");
                    // Оставляем targetResolution пустым, это нормально
                }

                var gatedTargetFiles = requestedNewFile is not null
                    ? new List<string>()
                    : targetResolution.IsResolved
                        ? targetResolution.SelectedFiles.ToList()
                        : null;

                // Create sandbox
                if (!await _sandboxManager.CreateSandbox())
                {
                    var error = "Failed to create sandbox";
                    _memory.Add("error", error, "SandboxCreationFailed");
                    return FinalizeRunResult(false, error, "Sandbox creation failed", "SANDBOX_CREATION_FAILED", Array.Empty<string>(), Array.Empty<ChangedHint>(), Array.Empty<ChangedRange>(), Array.Empty<ChangedKind>(), false);
                }

                string currentResponse = string.Empty;
                var changedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var changedHints = new Dictionary<string, ChangedHint>(StringComparer.OrdinalIgnoreCase);
                var changedRanges = new Dictionary<string, ChangedRange>(StringComparer.OrdinalIgnoreCase);
                var changedKinds = new Dictionary<string, ChangedKind>(StringComparer.OrdinalIgnoreCase);
                string? lastBuildErrorSignature = null;
                string? lastDeniedToolResult = null;
                var analysisOnlyTask = TaskPrecheckHeuristics.IsAnalysisOnlyTask(task);
                var runtimeClient = _llmClient as ILlmRuntimeClient;
                var runtimeMetadata = runtimeClient?.Metadata;
                var unrestrictedSandboxMode = AgentExecutionProfile.IsUnrestrictedInsideSandbox(_sessionContext);
                var actualIterationsUsed = 0;
                var lastSuccessfulStep = "Indexing";
                var lastKnownAction = "Indexing completed";
                var modelCallStarted = false;
                var patchStarted = false;
                var buildStarted = false;

                if (unrestrictedSandboxMode)
                {
                    _memory.Add("execution_profile", "UNRESTRICTED_INSIDE_SANDBOX");
                    tracer.LogActionEvent("ExecutionProfile", "Agent", ExecutionTracer.ActionLogLevel.Warning, "unrestricted_inside_sandbox", metadata: new Dictionary<string, object?>
                    {
                        { "access_mode", _sessionContext?.AccessMode.ToString() ?? string.Empty },
                        { "env_flag", "LOCALCURSOR_UNRESTRICTED_SANDBOX" }
                    });
                }

                tracer.LogActionEvent("IterationLoopStarted", "AgentIterationLoop", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
                {
                    { "max_iterations", MAX_ITERATIONS },
                    { "loop_stage", "AgentIterationLoop" }
                });

                for (int iteration = 0; iteration < MAX_ITERATIONS; iteration++)
                {
                    actualIterationsUsed = iteration + 1;
                    tracer.LogActionEvent("IterationStarted", "AgentIterationLoop", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
                    {
                        { "iteration", actualIterationsUsed },
                        { "max_iterations", MAX_ITERATIONS }
                    });

                    var semanticTopK = analysisOnlyTask ? 8 : 25 + CONTEXT_EXPANSION_BUFFER;
                    var candidateFiles = gatedTargetFiles ?? await _projectIndexer.FindRelevantFiles(task, semanticTopK);
                    lastSuccessfulStep = "RelevantFilesResolved";
                    lastKnownAction = $"Resolved {candidateFiles.Count} candidate files";

                    if (candidateFiles.Count > 0 && gatedTargetFiles == null)
                    {
                        _memory.Add("semantic_matches", string.Join(", ", candidateFiles));
                    }

                    var planningSignals = _contextBuilder.ComputeBudgetPlan(task, candidateFiles, new List<string>());
                    if (analysisOnlyTask)
                    {
                        planningSignals.Budget = Math.Min(planningSignals.Budget, 4);
                        planningSignals.MaxFiles = Math.Min(planningSignals.MaxFiles, 4);
                    }
                    _memory.Add("context_plan", $"{planningSignals.Complexity}:{planningSignals.Budget}:{planningSignals.Reason}");

                    List<string> resolvedFiles;
                    if (gatedTargetFiles != null)
                    {
                        resolvedFiles = gatedTargetFiles;
                    }
                    else
                    {
                        // Build context with adaptive budget and enforce target resolution before patching.
                        if (!_contextBuilder.TryResolveTarget(task, candidateFiles, new List<string>(), out resolvedFiles, out var failureMessage))
                        {
                            var safeFailure = failureMessage ?? "Target symbol not found in workspace";
                            _memory.Add("context_failure", safeFailure, "TargetResolutionSafeFailure");
                            _memory.Add("context_failure_reason", "Target resolution returned safe failure before patch generation", safeFailure);
                            tracer.MarkStopPoint("TargetResolutionGate", "TARGET_RESOLUTION_FAILED", safeFailure, new[] { "ModelRequest", "PatchApply", "BuildVerification" });
                            return FinalizeRunResult(false, safeFailure, "Target resolution failed before patch generation", "TARGET_RESOLUTION_FAILED", Array.Empty<string>(), Array.Empty<ChangedHint>(), Array.Empty<ChangedRange>(), Array.Empty<ChangedKind>(), false);
                        }
                    }

                    var contextInfo = _contextBuilder.BuildContext(task, resolvedFiles, new List<string>(), planningSignals.Budget);
                    lastSuccessfulStep = "ContextBuilt";
                    lastKnownAction = $"Built context with {resolvedFiles.Count} resolved files";
                    var contextString = analysisOnlyTask
                        ? AnalysisContextFormatter.BuildCompactAnalysisContext(contextInfo)
                        : _contextBuilder.FormatContext(contextInfo);

                    // Build prompt with context
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

                    // Call LLM
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
                    modelCallStarted = true;
                    var runtimeResult = runtimeClient is null
                        ? null
                        : await runtimeClient.GenerateNormalized(prompt, CancellationToken.None);
                    currentResponse = runtimeResult?.Completion ?? await _llmClient.Generate(prompt, CancellationToken.None);
                    tracer.LogActionEvent("ModelRequest", "Agent", ExecutionTracer.ActionLogLevel.Info, "completed", metadata: new Dictionary<string, object?>
                    {
                        { "operation_kind", "task_iteration" },
                        { "response_preview", currentResponse.Length > 200 ? currentResponse[..200] : currentResponse },
                        { "iteration", iteration }
                    }, durationMs: (long)(DateTime.UtcNow - modelStart).TotalMilliseconds);
                    lastSuccessfulStep = "ModelRequestCompleted";
                    lastKnownAction = "Model response received";
                    _memory.Add("llm_response", currentResponse.Length > 100 ? currentResponse.Substring(0, 100) + "..." : currentResponse);

                    var isHardFailure = runtimeResult?.IsFailure ?? LlmFailureDetector.IsHardLlmFailureResponse(currentResponse);
                    if (isHardFailure)
                    {
                        _memory.Add("llm_failure", currentResponse, "LlmUnavailableOrRequestFailed");
                        if (analysisOnlyTask)
                        {
                            var fallbackReason = FallbackReasonResolver.Resolve(runtimeResult, currentResponse, TimeoutResponseHeuristics.IsModelTimeoutResponse);
                            if (string.Equals(fallbackReason, "MODEL_TIMEOUT", StringComparison.Ordinal))
                            {
                                tracer.LogActionEvent("ModelCallTimedOut", "Agent", ExecutionTracer.ActionLogLevel.Warning, "timed_out", fallbackReason, metadata: new Dictionary<string, object?>
                                {
                                    { "iteration", iteration }
                                });
                            }

                            tracer.LogActionEvent("AnalysisFallbackStarted", "Agent", ExecutionTracer.ActionLogLevel.Warning, "started", fallbackReason, metadata: new Dictionary<string, object?>
                            {
                                { "fallback_mode", "INDEXED_CONTEXT_SUMMARY" },
                                { "provider_outcome", runtimeResult?.Status.ToString() ?? "legacy_classifier" },
                                { "response_length", currentResponse?.Length ?? 0 },
                                { "response_preview", string.IsNullOrWhiteSpace(currentResponse) ? string.Empty : (currentResponse.Length > 120 ? currentResponse[..120] : currentResponse) }
                            });
                            var fallbackSummary = AnalysisFallbackFormatter.BuildAnalysisFallbackSummary(contextInfo, fallbackReason);
                            tracer.LogActionEvent("AnalysisFallback", "Agent", ExecutionTracer.ActionLogLevel.Warning, "used", fallbackReason, metadata: new Dictionary<string, object?>
                            {
                                { "selected_files", contextInfo.SelectedFiles.ToArray() },
                                { "file_count", contextInfo.SelectedFiles.Count }
                            });
                            tracer.LogActionEvent("AnalysisFallbackCompleted", "Agent", ExecutionTracer.ActionLogLevel.Info, "completed", fallbackReason, metadata: new Dictionary<string, object?>
                            {
                                { "fallback_mode", "INDEXED_CONTEXT_SUMMARY" },
                                { "file_count", contextInfo.SelectedFiles.Count }
                            });

                            return FinalizeRunResult(
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
                        }

                        return FinalizeStructuredDiagnosticResult(
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
                    }

                    if (analysisOnlyTask &&
                        !string.IsNullOrWhiteSpace(currentResponse) &&
                        !currentResponse.TrimStart().StartsWith("TOOL:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TaskIntentClassifier.IsTechnicalAnalysisIntent(task) &&
                            (contextInfo.SelectedFiles.Count == 0 || NoToolResponseHeuristics.IsNonSubstantiveNoToolResponse(currentResponse) || NoToolResponseHeuristics.IsNeedsMoreDataResponse(currentResponse)))
                        {
                            _memory.Add("task_status", "needs_action_plan");
                            return FinalizeRunResult(
                                false,
                                "Technical analysis request did not produce grounded workspace analysis. Produce actionable target/context steps first.",
                                "No actionable steps produced for technical analysis intent",
                                "NO_ACTIONABLE_STEPS",
                                Array.Empty<string>(),
                                Array.Empty<ChangedHint>(),
                                Array.Empty<ChangedRange>(),
                                Array.Empty<ChangedKind>(),
                                false);
                        }

                        _memory.Add("final_response", currentResponse);
                        return FinalizeRunResult(
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
                    }

                    // Check for tool calls
                    if (_toolCaller.ContainsToolCalls(currentResponse))
                    {
                        var toolCalls = _toolCaller.ParseToolCalls(currentResponse);
                        lastSuccessfulStep = "ToolCallsParsed";
                        lastKnownAction = $"Parsed {toolCalls.Count} tool calls";
                        if (toolCalls.Count == 0)
                        {
                            if (NoToolResponseHeuristics.IsNonSubstantiveNoToolResponse(currentResponse))
                            {
                                currentResponse = "Your previous response did not contain the final analysis. Provide the final answer now. Do not say what you will do. Do not ask for more steps. Do not emit a tool call.";
                                continue;
                            }

                            if (!analysisOnlyTask && (TaskIntentClassifier.IsBroadEngineeringIntent(task) || TaskIntentClassifier.IsTechnicalAnalysisIntent(task)))
                            {
                                _memory.Add("task_status", "needs_action_plan");
                                return FinalizeRunResult(
                                    false,
                                    "The task requires an actionable engineering plan or concrete edits, but no tool/action step was produced.",
                                    "No actionable steps produced for broad engineering intent",
                                    "NO_ACTIONABLE_STEPS",
                                    Array.Empty<string>(),
                                    Array.Empty<ChangedHint>(),
                                    Array.Empty<ChangedRange>(),
                                    Array.Empty<ChangedKind>(),
                                    false);
                            }

                            _memory.Add("final_response", currentResponse);
                            return FinalizeRunResult(
                                true,
                                string.IsNullOrWhiteSpace(currentResponse) ? "Agent run completed successfully." : currentResponse,
                                analysisOnlyTask ? "Analysis response generated" : "Agent completed without tool calls",
                                analysisOnlyTask ? "SUCCESS_ANALYSIS_RESPONSE" : "SUCCESS_NO_TOOL_CALLS",
                                Array.Empty<string>(),
                                Array.Empty<ChangedHint>(),
                                Array.Empty<ChangedRange>(),
                                Array.Empty<ChangedKind>(),
                                false);
                        }

                        var mutationIntentTask = MutationIntentDetector.IsMutationIntentTask(task) || requestedNewFile != null;

                        var mutationCall = toolCalls.FirstOrDefault(ToolCallMutationHeuristics.IsMutationLikeToolCall);
                        if (mutationCall != null)
                        {
                            var intentGate = new IntentConfirmationGate(_contextBuilder.Tracer);
                            var intentDecision = intentGate.Evaluate(task, mutationCall.Input, targetResolution);
                            _memory.Add("intent_confirmation_gate", $"{intentDecision.ReasonCode}:{intentDecision.ClassifiedKind}:{intentDecision.Outcome}");

                            if (intentDecision.IsRejected)
                            {
                                var safeFailure = intentDecision.Reason;
                                _memory.Add("context_failure", safeFailure, intentDecision.ReasonCode);
                                tracer.MarkStopPoint("IntentConfirmationGate", intentDecision.ReasonCode, safeFailure, new[] { "MultiFileGate", "PatchApply", "BuildVerification" });
                                return FinalizeRunResult(
                                    false,
                                    safeFailure,
                                    $"Intent confirmation gate failed: {intentDecision.ReasonCode}",
                                    intentDecision.ReasonCode,
                                    Array.Empty<string>(),
                                    Array.Empty<ChangedHint>(),
                                    Array.Empty<ChangedRange>(),
                                    Array.Empty<ChangedKind>(),
                                    false);
                            }

                            var multiFileGate = new MultiFileEditGate(_contextBuilder.Tracer);
                            var multiFileDecision = multiFileGate.Evaluate(task, toolCalls, targetResolution, intentDecision);
                            _memory.Add("multi_file_edit_gate", $"{multiFileDecision.ReasonCode}:{multiFileDecision.ClassifiedKind}:{multiFileDecision.Outcome}");

                            if (multiFileDecision.IsRejected)
                            {
                                var safeFailure = multiFileDecision.Reason;
                                _memory.Add("context_failure", safeFailure, multiFileDecision.ReasonCode);
                                tracer.MarkStopPoint("MultiFileEditGate", multiFileDecision.ReasonCode, safeFailure, new[] { "PatchApply", "BuildVerification" });
                                return FinalizeRunResult(
                                    false,
                                    safeFailure,
                                    $"Multi-file edit gate failed: {multiFileDecision.ReasonCode}",
                                    multiFileDecision.ReasonCode,
                                    Array.Empty<string>(),
                                    Array.Empty<ChangedHint>(),
                                    Array.Empty<ChangedRange>(),
                                    Array.Empty<ChangedKind>(),
                                    false);
                            }
                        }

                        patchStarted = patchStarted || toolCalls.Any(ToolCallMutationHeuristics.IsMutationLikeToolCall);
                        var toolResults = await _toolCaller.ExecuteToolCalls(toolCalls);
                        lastSuccessfulStep = "ToolCallsExecuted";
                        lastKnownAction = $"Executed {toolCalls.Count} tool calls";
                        var unknownToolError = toolResults.FirstOrDefault(result =>
                            result.StartsWith("Error: Tool '", StringComparison.OrdinalIgnoreCase));
                        foreach (var result in toolResults)
                        {
                            _memory.Add("tool_result", result.Length > 100 ? result.Substring(0, 100) + "..." : result);

                            if (result.StartsWith("DENIED [", StringComparison.OrdinalIgnoreCase))
                            {
                                tracer.LogActionEvent("ToolResult", "Agent", ExecutionTracer.ActionLogLevel.Warning, "denied", metadata: new Dictionary<string, object?>
                                {
                                    { "tool_result", result }
                                });
                                if (string.Equals(lastDeniedToolResult, result, StringComparison.Ordinal))
                                {
                                    return FinalizeStructuredDiagnosticResult(
                                        "SAFE_REJECTION",
                                        new StructuredDiagnostic
                                        {
                                            RootCause = "Repeated safety-gate denial after a fix attempt.",
                                            AttemptedFix = mutationCall?.Input ?? "tool call denied",
                                            WhyDenied = result,
                                            NextSafeAction = "Regenerate a safer single-file write for the same target without changing unrelated files."
                                        },
                                        changedFiles,
                                        changedHints.Values,
                                        changedRanges.Values,
                                        changedKinds.Values);
                                }

                                lastDeniedToolResult = result;
                            }

                            // Mark files as Hot after patch, then re-index them
                            foreach (var call in toolCalls)
                            {
                                if (call.ToolName.Equals("file", StringComparison.OrdinalIgnoreCase) && 
                                    call.Input.StartsWith("write:", StringComparison.OrdinalIgnoreCase))
                                {
                                    var filePath = WriteTargetPathExtractor.ExtractWriteTargetPath(call.Input);
                                    if (!string.IsNullOrWhiteSpace(filePath))
                                    {
                                        changedFiles.Add(filePath);
                                        tracer.MarkChangedFile(filePath);
                                        var patchDecision = PatchDecisionBuilder.BuildPatchDecision(filePath, call.Input, resolvedFiles);
                                        _contextBuilder.Tracer.LogPatchDecision(patchDecision);
                                        changedHints[filePath] = ChangedHintComposer.BuildChangedHint(filePath, call.Input, patchDecision);
                                        var changedRange = BuildChangedRange(filePath, call.Input, patchDecision, _projectIndexer.SymbolDirectory);
                                        if (changedRange != null)
                                        {
                                            changedRanges[filePath] = changedRange;
                                        }
                                        changedKinds[filePath] = ChangedKindBuilder.BuildChangedKind(task, call.Input, patchDecision, buildResult: null);
                                        
                                        // Mark as Hot immediately after patch (highest priority)
                                        _fileStateManager.MarkHot(filePath);
                                        
                                        // Re-index will mark as Clean after successful embedding
                                        await _projectIndexer.ReindexFile(filePath);
                                    }
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(unknownToolError))
                        {
                            currentResponse = $@"Tool call rejected: {unknownToolError}

Use only the registered tools exactly as listed in the prompt. The only valid tool names are 'file' and 'build'. If the task is analysis-only, respond directly without any tool call.";
                            continue;
                        }

                        if (mutationCall != null)
                        {
                            // Tools currently mutate the active workspace directly, so verify the same workspace.
                            var buildPath = _sessionContext?.ActiveWorkspaceRoot;
                            if (!string.IsNullOrWhiteSpace(buildPath) && Directory.Exists(buildPath))
                            {
                                buildStarted = true;
                                var buildResult = await _buildVerifier.VerifyBuild(buildPath);
                                _contextBuilder.Tracer.LogBuildVerificationResult(buildResult);
                                lastSuccessfulStep = "BuildVerificationCompleted";
                                lastKnownAction = buildResult.Success ? "Build verification succeeded" : "Build verification failed";

                                if (buildResult.Success)
                                {
                                    _memory.Add("build_status", "success");

                                    if (changedFiles.Count == 0)
                                    {
                                        _memory.Add("task_status", "no_op_after_build");
                                        _sandboxManager.CleanupSandbox();
                                        return FinalizeRunResult(
                                            true,
                                            "Build succeeded but no file changes were made.",
                                            "Agent completed with no-op after successful build",
                                            "NO_OP_SUCCESS",
                                            Array.Empty<string>(),
                                            Array.Empty<ChangedHint>(),
                                            Array.Empty<ChangedRange>(),
                                            Array.Empty<ChangedKind>(),
                                            true);
                                    }

                                    if (VERBOSE_OUTPUT)
                                        Console.WriteLine("✓ Changes are already written in the active workspace");
                                    _memory.Add("task_status", "completed");
                                    _sandboxManager.CleanupSandbox();
                                    return FinalizeRunResult(
                                        true,
                                        "Task completed successfully",
                                        "Agent completed task in the active workspace",
                                        "SUCCESS",
                                        changedFiles,
                                        changedHints.Values,
                                        changedRanges.Values,
                                        changedKinds.Values,
                                        true);
                                }
                                else
                                {
                                    var errorMessage = string.Join("\n", buildResult.Errors);
                                    _memory.Add("build_errors", errorMessage, "BuildVerificationFailed");

                                    if (TryRepairCs8802(buildResult, changedFiles, out var repairPrompt))
                                    {
                                        currentResponse = repairPrompt ?? "Repaired CS8802-related issue. Re-run build and continue.";
                                        continue;
                                    }

                                    if (string.Equals(lastBuildErrorSignature, errorMessage, StringComparison.Ordinal))
                                    {
                                        return FinalizeStructuredDiagnosticResult(
                                            "BUILD_FAILED_AFTER_PATCH",
                                            new StructuredDiagnostic
                                            {
                                                RootCause = "The same build failure repeated after a fix attempt.",
                                                AttemptedFix = mutationCall.Input,
                                                WhyDenied = errorMessage,
                                                NextSafeAction = "Inspect the compiler error and regenerate one targeted edit that directly addresses it."
                                            },
                                            changedFiles,
                                            changedHints.Values,
                                            changedRanges.Values,
                                            changedKinds.Values);
                                    }

                                    lastBuildErrorSignature = errorMessage;

                                    // Provide error context to LLM for next iteration
                                    currentResponse = $"Build errors encountered:\n{errorMessage}\n\nPlease fix these errors.";
                                }
                            }
                        }

                        if (analysisOnlyTask && mutationCall == null)
                        {
                            currentResponse = "You have enough context. Provide a final answer now with no tool call.";
                        }
                        else if (mutationIntentTask && mutationCall == null && changedFiles.Count == 0)
                        {
                            currentResponse = requestedNewFile != null
                                ? $"This is a new-file creation task. Use the file tool now with write:{requestedNewFile}:... and create the requested file directly. Do not ask for clarification and do not answer without a tool call."
                                : "This is a code change task. Make one concrete code edit now using the file tool. Do not keep reading or building without writing a change.";
                        }
                        else
                        {
                            // Add feedback for next iteration
                            currentResponse += "\n\nContinue implementing the task if not complete.";
                        }
                    }
                    else
                    {
                        // No tool calls - this might be the final response
                        if (MutationIntentDetector.IsMutationIntentTask(task) || requestedNewFile != null)
                        {
                            currentResponse = requestedNewFile != null
                                ? $"This is a file creation task. Use the file tool now to write:{requestedNewFile}:... and create the requested file. Do not answer with explanation only."
                                : "This is a code change task. Use the file tool now to write Program.cs and make a concrete edit. Do not answer with code only.";
                            continue;
                        }

                        if (NoToolResponseHeuristics.IsNonSubstantiveNoToolResponse(currentResponse))
                        {
                            currentResponse = "Your previous response did not contain the final analysis. Provide the final answer now. Do not say what you will do. Do not ask for more steps. Do not emit a tool call.";
                            continue;
                        }

                        _memory.Add("final_response", currentResponse);
                        if (TaskIntentClassifier.IsBroadEngineeringIntent(task) || TaskIntentClassifier.IsTechnicalAnalysisIntent(task))
                        {
                            _memory.Add("task_status", "needs_action_plan");
                            return FinalizeRunResult(
                                false,
                                "The task requires an actionable engineering plan or concrete edits, but no tool/action step was produced.",
                                "No actionable steps produced for broad engineering intent",
                                "NO_ACTIONABLE_STEPS",
                                Array.Empty<string>(),
                                Array.Empty<ChangedHint>(),
                                Array.Empty<ChangedRange>(),
                                Array.Empty<ChangedKind>(),
                                false);
                        }

                        return FinalizeRunResult(
                            true,
                            string.IsNullOrWhiteSpace(currentResponse) ? "Agent run completed successfully." : currentResponse,
                            "Agent completed without tool calls",
                            "SUCCESS_NO_TOOL_CALLS",
                            Array.Empty<string>(),
                            Array.Empty<ChangedHint>(),
                            Array.Empty<ChangedRange>(),
                            Array.Empty<ChangedKind>(),
                            false);
                    }

                    tracer.LogActionEvent("IterationCompleted", "AgentIterationLoop", ExecutionTracer.ActionLogLevel.Info, "completed", metadata: new Dictionary<string, object?>
                    {
                        { "iteration", actualIterationsUsed },
                        { "max_iterations", MAX_ITERATIONS },
                        { "last_successful_step", lastSuccessfulStep },
                        { "last_known_action", lastKnownAction }
                    });
                }

                var finalMessage = "Max iterations reached. Task may not be fully complete.";
                _memory.Add("task_status", "max_iterations");
                tracer.LogActionEvent("MaxIterationsReached", "AgentIterationLoop", ExecutionTracer.ActionLogLevel.Warning, "failed", "MAX_ITERATIONS_REACHED", new Dictionary<string, object?>
                {
                    { "loop_stage", "AgentIterationLoop" },
                    { "max_iterations", MAX_ITERATIONS },
                    { "iterations_used", actualIterationsUsed },
                    { "last_successful_step", lastSuccessfulStep },
                    { "failed_step", "MaxIterationsReached" },
                    { "last_known_action", lastKnownAction },
                    { "model_call_started", modelCallStarted },
                    { "patch_started", patchStarted },
                    { "build_started", buildStarted },
                    { "pipeline_stopped_reason", "Iteration budget exhausted before completion" }
                });
                tracer.MarkStopPoint("AgentIterationLoop", "MAX_ITERATIONS_REACHED", finalMessage, buildStarted ? Array.Empty<string>() : new[] { "BuildVerification" });
                tracer.LogActionEvent("RunFailedWithRootCause", "Agent", ExecutionTracer.ActionLogLevel.Warning, "failed", "MAX_ITERATIONS_REACHED", new Dictionary<string, object?>
                {
                    { "root_cause_code", "MAX_ITERATIONS_REACHED" },
                    { "failed_stage", "AgentIterationLoop" },
                    { "failed_step", "MaxIterationsReached" }
                });
                return FinalizeRunResult(
                    false,
                    finalMessage,
                    "Maximum iterations reached",
                    "MAX_ITERATIONS_REACHED",
                    Array.Empty<string>(),
                    Array.Empty<ChangedHint>(),
                    Array.Empty<ChangedRange>(),
                    Array.Empty<ChangedKind>(),
                    false,
                    buildStarted: buildStarted,
                    failure: new FailurePayload
                    {
                        RootCauseCode = "MAX_ITERATIONS_REACHED",
                        FailedStage = "AgentIterationLoop",
                        LastSuccessfulStep = lastSuccessfulStep,
                        FailedStep = "MaxIterationsReached",
                        ReasonCode = "MAX_ITERATIONS_REACHED",
                        Explanation = finalMessage,
                        PipelineStoppedReason = "Iteration budget exhausted before completion",
                        DownstreamNotStarted = buildStarted ? string.Empty : "BuildVerification",
                        LoopStage = "AgentIterationLoop",
                        MaxIterations = MAX_ITERATIONS,
                        IterationsUsed = actualIterationsUsed,
                        LastKnownAction = lastKnownAction,
                        ModelCallStarted = modelCallStarted,
                        PatchStarted = patchStarted,
                        BuildStarted = buildStarted,
                        Timeline = TimelineBuilder.BuildMaxIterationsTimeline(actualIterationsUsed, MAX_ITERATIONS, lastSuccessfulStep, lastKnownAction)
                    });
            }
            catch (Exception ex)
            {
                var error = $"Agent error: {ex.Message}";
                _memory.Add("error", error, "UnhandledException");
                tracer.LogActionEvent("TaskLifecycle", "Agent", ExecutionTracer.ActionLogLevel.Error, "failed", "UNHANDLED_EXCEPTION", new Dictionary<string, object?>
                {
                    { "exception", ex.ToString() }
                });
                return FinalizeRunResult(false, error, "Unhandled exception", "UNHANDLED_EXCEPTION", Array.Empty<string>(), Array.Empty<ChangedHint>(), Array.Empty<ChangedRange>(), Array.Empty<ChangedKind>(), false);
            }
            finally
            {
                _sandboxManager.CleanupSandbox();
            }
        }
        internal enum ChangedKindType
        {
            BugFix,
            Validation,
            Refactor,
            BuildFix,
            FeatureAdd,
            Update,
            Unknown
        }

        private bool TryRepairCs8802(BuildVerifier.BuildResult buildResult, HashSet<string> changedFiles, out string? nextPrompt)
        {
            nextPrompt = null;
            var cs8802 = buildResult.Errors.FirstOrDefault(e => e.Contains("CS8802", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(cs8802) || _sessionContext == null)
                return false;

            var programPath = Path.Combine(_sessionContext.ActiveWorkspaceRoot, "Program.cs");
            if (!File.Exists(programPath) || !TopLevelStatementInspector.ContainsTopLevelStatements(programPath))
                return false;

            foreach (var changedFile in changedFiles.Where(f =>
                         f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                         !Path.GetFileName(f).Equals("Program.cs", StringComparison.OrdinalIgnoreCase)))
            {
                if (!File.Exists(changedFile))
                    continue;

                var fileText = File.ReadAllText(changedFile);
                if (!TopLevelStatementInspector.ContainsMainEntryPoint(fileText))
                    continue;

                var normalized = TopLevelStatementInspector.NormalizeHelperClassWithoutMain(fileText);
                if (string.Equals(normalized, fileText, StringComparison.Ordinal))
                    continue;

                File.WriteAllText(changedFile, normalized);
                _fileStateManager.MarkHot(changedFile);
                nextPrompt = $"Detected CS8802 with top-level Program.cs. Removed Main entry point from {Path.GetFileName(changedFile)} and normalized it into helper/class form. Re-run build and continue fixing only remaining errors.";
                return true;
            }

            nextPrompt = "Detected CS8802 with top-level Program.cs. Do not rewrite Program.cs by default. Inspect newly created .cs files and remove any extra Main entry point or top-level executable code from them.";
            return true;
        }

        private static string? ExtractRequestedNewFilePath(string task)
        {
            return NewFilePathExtractor.ExtractRequestedNewFilePath(task);
        }

        private string FinalizeRunResult(
            bool ok,
            string message,
            string summary,
            string reasonCode,
            IEnumerable<string> changedFiles,
            IEnumerable<ChangedHint> changedHints,
            IEnumerable<ChangedRange> changedRanges,
            IEnumerable<ChangedKind> changedKinds,
            bool buildSucceeded,
            string? cancelSource = null,
            bool? buildStarted = null,
            FailurePayload? failure = null,
            DateTime? runStartedUtc = null,
            string? workspace = null,
            string? provider = null,
            string? model = null,
            IEnumerable<string>? degradedFlags = null,
            string? fallbackReason = null,
            string? fallbackMode = null,
            string? payloadFinalStatus = null,
            TimelinePayload[]? timeline = null)
        {
            var tracerFinalStatus = cancelSource != null ? "cancelled" : ok ? "succeeded" : "failed";
            foreach (var file in changedFiles)
            {
                _contextBuilder.Tracer.MarkChangedFile(file);
            }

            _contextBuilder.Tracer.CompleteRun(tracerFinalStatus, summary, reasonCode, buildSucceeded, cancelSource);
            return EmitAgentRunResult(
                ok,
                message,
                summary,
                reasonCode,
                changedFiles,
                changedHints,
                changedRanges,
                changedKinds,
                buildSucceeded,
                buildStarted ?? buildSucceeded,
                failure,
                runStartedUtc,
                workspace,
                provider,
                model,
                degradedFlags,
                fallbackReason,
                fallbackMode,
                payloadFinalStatus,
                timeline,
                _contextBuilder.Tracer.GetApprovalRequiredActions(),
                _contextBuilder.Tracer.GetDeniedPermissionDecisionCount(),
                _contextBuilder.Tracer.GetActionLedger());
        }

        private string FinalizeStructuredDiagnosticResult(string reasonCode, StructuredDiagnostic diagnostic, IEnumerable<string> changedFiles, IEnumerable<ChangedHint> changedHints, IEnumerable<ChangedRange> changedRanges, IEnumerable<ChangedKind> changedKinds)
        {
            _contextBuilder.Tracer.MarkStopPoint("Agent", reasonCode, diagnostic.WhyDenied, Array.Empty<string>());
            _contextBuilder.Tracer.CompleteRun("failed", "Agent stopped with structured diagnostic", reasonCode, false);
            var message = StructuredDiagnosticMessageBuilder.Build(diagnostic);

            return EmitAgentRunResult(
                false,
                message,
                "Agent stopped with structured diagnostic",
                string.Empty,
                changedFiles,
                changedHints,
                changedRanges,
                changedKinds,
                false,
                false,
                failure: null,
                runStartedUtc: null,
                workspace: null,
                provider: null,
                model: null,
                degradedFlags: null,
                fallbackReason: null,
                fallbackMode: null,
                finalStatus: null,
                timeline: null,
                approvalRequiredActions: Array.Empty<ActionApprovalProposal>(),
                tracerDeniedActions: 0,
                actionLifecycleEntries: Array.Empty<ActionLifecycleEntry>());
        }

        private static ChangedRange? BuildChangedRange(string filePath, string toolInput, ExecutionTracer.PatchDecision patchDecision, ProjectSymbolDirectory? symbolDirectory)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            var lines = File.ReadAllLines(filePath);
            if (lines.Length is 0)
            {
                return null;
            }

            var candidates = new List<string>(3);

            if (patchDecision.TargetMethod is { Length: > 0 })
            {
                candidates.Add(patchDecision.TargetMethod);
            }

            if (ChangeHintBuilder.ExtractTargetSymbol(toolInput) is { Length: > 0 } targetSymbol)
            {
                candidates.Add(targetSymbol);
            }

            var fallbackName = Path.GetFileNameWithoutExtension(filePath);
            if (fallbackName is { Length: > 0 })
            {
                candidates.Add(fallbackName);
            }

            var indexedSymbols = symbolDirectory?.GetSymbols(filePath) ?? [];

            var uniqueCandidates = candidates.Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in uniqueCandidates)
            {
                var symbolRange = FindBestSymbolRangeForFile(lines, indexedSymbols, candidate);
                if (!symbolRange.HasValue)
                {
                    continue;
                }

                var (startLine, endLine) = symbolRange.Value;
                return new ChangedRange
                {
                    File = filePath,
                    StartLine = startLine,
                    EndLine = endLine
                };
            }

            foreach (var candidate in uniqueCandidates)
            {
                var lineIndex = FindMatchingLine(lines, candidate);
                if (lineIndex >= 0)
                {
                    var enclosingRange = FindNearestEnclosingSymbolRange(lines, lineIndex);
                    if (enclosingRange is not null)
                    {
                        return new ChangedRange
                        {
                            File = filePath,
                            StartLine = enclosingRange.Value.startLine,
                            EndLine = enclosingRange.Value.endLine
                        };
                    }

                    var startLine = Math.Max(1, lineIndex + 1);
                    return new ChangedRange
                    {
                        File = filePath,
                        StartLine = startLine,
                        EndLine = startLine
                    };
                }
            }

            return null;
        }

        private static (int startLine, int endLine)? FindBestSymbolRangeForFile(string[] lines, List<string> indexedSymbols, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate) || lines.Length is 0)
                return null;

            var searchOrder = indexedSymbols
                .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!searchOrder.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                searchOrder.Insert(0, candidate);
            }

            foreach (var symbol in searchOrder)
            {
                var symbolLine = FindSymbolDeclarationLine(lines, symbol);
                if (symbolLine is < 0) continue;

                var methodStart = FindNearestDeclarationStart(lines, anchorLineIndex: symbolLine, declarationKind: "method");
                if (methodStart is >= 0)
                {
                    var methodEnd = FindMatchingBlockEnd(lines, methodStart);
                    var methodStartLine = methodStart + 1;
                    var methodEndLine = Math.Max(methodStartLine, methodEnd + 1);
                    return (methodStartLine, methodEndLine);
                }

                var classStart = FindNearestDeclarationStart(lines, anchorLineIndex: symbolLine, declarationKind: "class");
                if (classStart is >= 0)
                {
                    var classEnd = FindMatchingBlockEnd(lines, classStart);
                    var classStartLine = classStart + 1;
                    var classEndLine = Math.Max(classStartLine, classEnd + 1);
                    return (classStartLine, classEndLine);
                }

                var startLine = Math.Max(1, symbolLine + 1);
                return (startLine, startLine);
            }

            return null;
        }

        private static (int startLine, int endLine)? FindNearestEnclosingSymbolRange(string[] lines, int anchorLineIndex)
        {
            if (lines.Length == 0)
                return null;

            var methodStart = FindNearestDeclarationStart(lines, anchorLineIndex: anchorLineIndex, declarationKind: "method");
            if (methodStart is >= 0)
            {
                var methodEnd = FindMatchingBlockEnd(lines, methodStart);
                return (methodStart + 1, Math.Max(methodStart + 1, methodEnd + 1));
            }

            var classStart = FindNearestDeclarationStart(lines, anchorLineIndex: anchorLineIndex, declarationKind: "class");
            if (classStart is >= 0)
            {
                var classEnd = FindMatchingBlockEnd(lines, classStart);
                return (classStart + 1, Math.Max(classStart + 1, classEnd + 1));
            }

            return null;
        }

        private static int FindNearestDeclarationStart(string[] lines, int anchorLineIndex, string declarationKind)
        {
            if (lines.Length == 0)
                return -1;

            const StringComparison KindComparison = StringComparison.OrdinalIgnoreCase;
            var isMethod = declarationKind.Equals("method", KindComparison);
            var isClass = declarationKind.Equals("class", KindComparison);
            var startIndex = Math.Min(anchorLineIndex, lines.Length - 1);
            if (!isMethod && !isClass)
                return -1;

            for (var i = startIndex; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (isMethod)
                {
                    if (LooksLikeMethodDeclaration(line))
                    {
                        return i;
                    }
                }

                if (isClass && LooksLikeClassDeclaration(line))
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool LooksLikeMethodDeclaration(string line)
        {
            return !string.IsNullOrWhiteSpace(line) &&
                   Regex.IsMatch(line, @"(?:public|private|protected|internal)(?:\s+static)?(?:\s+async)?\s+(?:virtual\s+)?(?:override\s+)?[\w<>\.\[\],]+\s+\w+\s*\(");
        }

        private static bool LooksLikeClassDeclaration(string line)
        {
            return !string.IsNullOrWhiteSpace(line) &&
                   Regex.IsMatch(line, @"(?:public|private|protected|internal)?\s*(?:abstract|sealed|static)?\s*(?:partial\s+)?class\s+\w+");
        }

        private static int FindMatchingBlockEnd(string[] lines, int startLineIndex)
        {
            var braceDepth = 0;
            var seenOpeningBrace = false;
            const int openingBraceDelta = 1;
            const int closingBraceDelta = -1;

            for (var i = startLineIndex; i < lines.Length; i++)
            {
                var line = lines[i];
                for (var j = 0; j < line.Length; j++)
                {
                    switch (line[j])
                    {
                        case '{':
                            braceDepth += openingBraceDelta;
                            seenOpeningBrace = true;
                            break;
                        case '}':
                            braceDepth += closingBraceDelta;
                            if (seenOpeningBrace && braceDepth <= 0)
                                return i;
                            break;
                    }
                }
            }

            return startLineIndex;
        }

        private static int FindSymbolDeclarationLine(string[] lines, string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return -1;

            const StringComparison SymbolComparison = StringComparison.Ordinal;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (LooksLikeMethodDeclaration(line) && line.Contains(symbol, SymbolComparison))
                {
                    return i;
                }
                else if (LooksLikeClassDeclaration(line) && line.Contains(symbol, SymbolComparison))
                {
                    return i;
                }
            }

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(symbol, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindMatchingLine(string[] lines, string needle)
        {
            const int NotFound = -1;
            if (string.IsNullOrWhiteSpace(needle))
            {
                return NotFound;
            }
            if (lines.Length == 0)
            {
                return NotFound;
            }

            const StringComparison NeedleComparison = StringComparison.OrdinalIgnoreCase;
            var lineCount = lines.Length;
            var localNeedle = needle;
            var localLines = lines;
            var notFound = NotFound;
            var needleComparison = NeedleComparison;
            var localLineCount = lineCount;
            var startIndex = 0;
            var localIndex = startIndex;
            var loopStart = localIndex;
            var loopEnd = localLineCount;
            var index = loopStart;
            var loopIndex = index;
            var scanIndex = loopIndex;
            for (var i = scanIndex; i < loopEnd; i++)
            {
                if (localLines[i].IndexOf(localNeedle, needleComparison) >= 0)
                {
                    return i;
                }
            }
            return notFound;
        }

        private string BuildPromptWithContext(string task, int iteration, string previousResponse, string codeContext, string regressionAdvice, string promptShapingAdvice, string strategyBiasAdvice)
        {
            var executionContext = _memory.GetContextString(CONTEXT_WINDOW);
            var taskProfile = _memorySystem.GetTaskProfileSummary(task);
            var toolsDescription = _toolRegistry.GetToolsDescription();
            var policyBlock = WorkspacePolicyFormatter.BuildPolicyBlock(_sessionContext);
            var startupStateBlock = StartupStateFormatter.BuildStartupStateBlock(_sessionContext, _workspaceResolution);
            return ContextPromptBuilder.BuildPromptWithContext(
                task,
                iteration,
                previousResponse,
                codeContext,
                regressionAdvice,
                promptShapingAdvice,
                strategyBiasAdvice,
                executionContext,
                taskProfile,
                toolsDescription,
                policyBlock,
                startupStateBlock,
                ResponseLanguageHelper.BuildResponseLanguageRule(task));
        }

        private static string EmitAgentRunResult(
            bool ok,
            string message,
            string summary,
            string reasonCode,
            IEnumerable<string> changedFiles,
            IEnumerable<ChangedHint> changedHints,
            IEnumerable<ChangedRange> changedRanges,
            IEnumerable<ChangedKind> changedKinds,
            bool buildSucceeded,
            bool buildStarted,
            FailurePayload? failure,
            DateTime? runStartedUtc,
            string? workspace,
            string? provider,
            string? model,
            IEnumerable<string>? degradedFlags,
            string? fallbackReason,
            string? fallbackMode,
            string? finalStatus,
            TimelinePayload[]? timeline,
            IReadOnlyList<ActionApprovalProposal> approvalRequiredActions,
            int tracerDeniedActions,
            IReadOnlyList<ActionLifecycleEntry> actionLifecycleEntries)
        {
            var effectiveReasonCode = (failure?.ReasonCode ?? reasonCode) ?? string.Empty;
            var planRequired = string.Equals(effectiveReasonCode, "NO_ACTIONABLE_STEPS", StringComparison.OrdinalIgnoreCase);
            var continuationHint = ContinuationGuidanceBuilder.BuildContinuationHint(planRequired, effectiveReasonCode, failure?.LastKnownAction ?? string.Empty);
            var continuationStep = failure?.LastSuccessfulStep ?? string.Empty;
            var continuationAction = failure?.LastKnownAction ?? string.Empty;
            var nextActionCandidates = ContinuationGuidanceBuilder.BuildNextActionCandidates(planRequired, effectiveReasonCode, continuationHint, continuationAction);
            var normalizedChangedFiles = changedFiles
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var normalizedChangedHints = changedHints
                .Where(h => h != null && !string.IsNullOrWhiteSpace(h.File) && !string.IsNullOrWhiteSpace(h.Hint))
                .GroupBy(h => h.File, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .Select(h => new ChangedHintPayload
                {
                    File = h.File,
                    Hint = h.Hint
                })
                .ToArray();

            if (normalizedChangedHints.Length == 0 && normalizedChangedFiles.Length > 0)
            {
                normalizedChangedHints = normalizedChangedFiles.Select(file => new ChangedHintPayload
                {
                    File = file,
                    Hint = "Updated by agent"
                }).ToArray();
            }

            var normalizedChangedRanges = changedRanges
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.File) && r.StartLine > 0)
                .GroupBy(r => r.File, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .Select(r => new ChangedRangePayload
                {
                    File = r.File,
                    StartLine = r.StartLine,
                    EndLine = r.EndLine > 0 ? r.EndLine : r.StartLine
                })
                .ToArray();

            var normalizedChangedKinds = changedKinds
                .Where(k => k != null && !string.IsNullOrWhiteSpace(k.File))
                .GroupBy(k => k.File, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .Select(k => new ChangedKindPayload
                {
                    File = k.File,
                    Kind = k.Kind
                })
                .ToArray();

            var payload = new AgentRunResultPayload
            {
                Ok = ok,
                Message = message,
                Summary = summary,
                ChangedFiles = normalizedChangedFiles,
                ChangedHints = normalizedChangedHints,
                ChangedRanges = normalizedChangedRanges,
                ChangedKinds = normalizedChangedKinds,
                Workspace = workspace ?? string.Empty,
                DurationMs = runStartedUtc.HasValue ? Math.Max(0, (long)(DateTime.UtcNow - runStartedUtc.Value).TotalMilliseconds) : null,
                RuntimeElapsedMs = runStartedUtc.HasValue ? Math.Max(0, (long)(DateTime.UtcNow - runStartedUtc.Value).TotalMilliseconds) : null,
                Provider = provider ?? string.Empty,
                Model = model ?? string.Empty,
                RuntimeProfile = RuntimeTuningResolver.ResolveRuntimeProfileId(provider, model),
                RuntimeEndpoint = RuntimeTuningResolver.ResolveRuntimeEndpoint(provider),
                ConfiguredContextWindow = RuntimeTuningResolver.ResolveConfiguredContextWindow(provider),
                ConfiguredGpuOffloadOptions = RuntimeTuningResolver.ResolveConfiguredGpuOffloadOptions(provider),
                RuntimeTuningProfile = RuntimeTuningResolver.ResolveRuntimeTuningProfile(provider, model),
                RuntimeTuningOptions = RuntimeTuningResolver.ResolveRuntimeTuningOptions(provider, model),
                RuntimeTuningSource = RuntimeTuningResolver.ResolveRuntimeTuningSource(provider, model),
                RuntimeTuningApplied = RuntimeTuningResolver.ResolveRuntimeTuningApplied(provider, model),
                RuntimeTuningWarnings = RuntimeTuningResolver.ResolveRuntimeTuningWarnings(provider, model),
                GpuUsageMeasured = false,
                DegradedFlags = (degradedFlags ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                FallbackReason = fallbackReason ?? string.Empty,
                FallbackMode = fallbackMode ?? string.Empty,
                FinalStatus = finalStatus ?? string.Empty,
                BuildSucceeded = buildSucceeded,
                BuildStarted = buildStarted,
                PlanRequired = planRequired,
                ContinuationHint = continuationHint,
                SessionContinuation = new SessionContinuationPayload
                {
                    LastSuccessfulStep = continuationStep,
                    LastKnownAction = continuationAction
                },
                NextActionCandidates = nextActionCandidates,
                RootCauseCode = failure?.RootCauseCode ?? (!ok ? reasonCode : string.Empty),
                FailedStage = failure?.FailedStage ?? string.Empty,
                LastSuccessfulStep = failure?.LastSuccessfulStep ?? string.Empty,
                FailedStep = failure?.FailedStep ?? string.Empty,
                ReasonCode = failure?.ReasonCode ?? reasonCode,
                Explanation = failure?.Explanation ?? string.Empty,
                PipelineStoppedReason = failure?.PipelineStoppedReason ?? string.Empty,
                DownstreamNotStarted = failure?.DownstreamNotStarted ?? string.Empty,
                LoopStage = failure?.LoopStage ?? string.Empty,
                MaxIterations = failure?.MaxIterations,
                IterationsUsed = failure?.IterationsUsed,
                LastKnownAction = failure?.LastKnownAction ?? string.Empty,
                ModelCallStarted = failure?.ModelCallStarted,
                PatchStarted = failure?.PatchStarted,
                Timeline = timeline ?? failure?.Timeline ?? Array.Empty<TimelinePayload>(),
                ApprovalRequiredActions = ApprovalProposalMapper.MapApprovalProposals(approvalRequiredActions),
                ExternalAttempts = approvalRequiredActions.Count,
                OutsideBoundaryAttempts = approvalRequiredActions.Count(x => x is not null && !x.IsInsideSandbox),
                HighRiskApprovalRequiredActions = approvalRequiredActions.Count(x => x is not null && string.Equals(x.RiskLevel, "high", StringComparison.OrdinalIgnoreCase)),
                DeniedActions = tracerDeniedActions,
                RequestedActions = actionLifecycleEntries.Count(e => e.LifecycleState == ActionLifecycleState.Requested),
                BlockedActions = actionLifecycleEntries.Count(e => e.LifecycleState == ActionLifecycleState.Blocked),
                ExecutedActions = actionLifecycleEntries.Count(e => e.LifecycleState == ActionLifecycleState.Executed),
                FailedActions = actionLifecycleEntries.Count(e => e.LifecycleState == ActionLifecycleState.Failed),
                HostBoundaryPreserved = true,
                ActionLifecycle = ActionLifecycleMapper.MapActionLifecycle(actionLifecycleEntries),
                ApprovalStatusSummary = ApprovalStatusSummaryBuilder.Build(actionLifecycleEntries)
            };

            Console.WriteLine(JsonSerializer.Serialize(payload));
            return message;
        }

        private sealed class AgentRunResultPayload
        {
            [JsonPropertyName("ok")]
            public bool Ok { get; init; }

            [JsonPropertyName("message")]
            public string Message { get; init; } = string.Empty;

            [JsonPropertyName("summary")]
            public string Summary { get; init; } = string.Empty;

            [JsonPropertyName("changedFiles")]
            public string[] ChangedFiles { get; init; } = Array.Empty<string>();

            [JsonPropertyName("changedHints")]
            public ChangedHintPayload[] ChangedHints { get; init; } = Array.Empty<ChangedHintPayload>();

            [JsonPropertyName("changedRanges")]
            public ChangedRangePayload[] ChangedRanges { get; init; } = Array.Empty<ChangedRangePayload>();

            [JsonPropertyName("changedKinds")]
            public ChangedKindPayload[] ChangedKinds { get; init; } = Array.Empty<ChangedKindPayload>();

            [JsonPropertyName("workspace")]
            public string Workspace { get; init; } = string.Empty;

            [JsonPropertyName("durationMs")]
            public long? DurationMs { get; init; }

            [JsonPropertyName("runtimeElapsedMs")]
            public long? RuntimeElapsedMs { get; init; }

            [JsonPropertyName("provider")]
            public string Provider { get; init; } = string.Empty;

            [JsonPropertyName("model")]
            public string Model { get; init; } = string.Empty;

            [JsonPropertyName("runtimeProfile")]
            public string RuntimeProfile { get; init; } = string.Empty;

            [JsonPropertyName("runtimeEndpoint")]
            public string RuntimeEndpoint { get; init; } = string.Empty;

            [JsonPropertyName("configuredContextWindow")]
            public string ConfiguredContextWindow { get; init; } = string.Empty;

            [JsonPropertyName("configuredGpuOffloadOptions")]
            public string ConfiguredGpuOffloadOptions { get; init; } = string.Empty;

            [JsonPropertyName("runtimeTuningProfile")]
            public string RuntimeTuningProfile { get; init; } = string.Empty;

            [JsonPropertyName("runtimeTuningOptions")]
            public string RuntimeTuningOptions { get; init; } = string.Empty;

            [JsonPropertyName("runtimeTuningSource")]
            public string RuntimeTuningSource { get; init; } = string.Empty;

            [JsonPropertyName("runtimeTuningApplied")]
            public bool RuntimeTuningApplied { get; init; }

            [JsonPropertyName("runtimeTuningWarnings")]
            public string[] RuntimeTuningWarnings { get; init; } = Array.Empty<string>();

            [JsonPropertyName("gpuUsageMeasured")]
            public bool GpuUsageMeasured { get; init; }

            [JsonPropertyName("degradedFlags")]
            public string[] DegradedFlags { get; init; } = Array.Empty<string>();

            [JsonPropertyName("fallbackReason")]
            public string FallbackReason { get; init; } = string.Empty;

            [JsonPropertyName("fallbackMode")]
            public string FallbackMode { get; init; } = string.Empty;

            [JsonPropertyName("finalStatus")]
            public string FinalStatus { get; init; } = string.Empty;

            [JsonPropertyName("buildSucceeded")]
            public bool BuildSucceeded { get; init; }

            [JsonPropertyName("buildStarted")]
            public bool BuildStarted { get; init; }

            [JsonPropertyName("planRequired")]
            public bool PlanRequired { get; init; }
            [JsonPropertyName("continuationHint")]
            public string ContinuationHint { get; init; } = string.Empty;
            [JsonPropertyName("sessionContinuation")]
            public SessionContinuationPayload SessionContinuation { get; init; } = new();
            [JsonPropertyName("nextActionCandidates")]
            public string[] NextActionCandidates { get; init; } = Array.Empty<string>();

            [JsonPropertyName("rootCauseCode")]
            public string RootCauseCode { get; init; } = string.Empty;

            [JsonPropertyName("failedStage")]
            public string FailedStage { get; init; } = string.Empty;

            [JsonPropertyName("lastSuccessfulStep")]
            public string LastSuccessfulStep { get; init; } = string.Empty;

            [JsonPropertyName("failedStep")]
            public string FailedStep { get; init; } = string.Empty;

            [JsonPropertyName("reasonCode")]
            public string ReasonCode { get; init; } = string.Empty;

            [JsonPropertyName("explanation")]
            public string Explanation { get; init; } = string.Empty;

            [JsonPropertyName("pipelineStoppedReason")]
            public string PipelineStoppedReason { get; init; } = string.Empty;

            [JsonPropertyName("downstreamNotStarted")]
            public string DownstreamNotStarted { get; init; } = string.Empty;

            [JsonPropertyName("loopStage")]
            public string LoopStage { get; init; } = string.Empty;

            [JsonPropertyName("maxIterations")]
            public int? MaxIterations { get; init; }

            [JsonPropertyName("iterationsUsed")]
            public int? IterationsUsed { get; init; }

            [JsonPropertyName("lastKnownAction")]
            public string LastKnownAction { get; init; } = string.Empty;

            [JsonPropertyName("modelCallStarted")]
            public bool? ModelCallStarted { get; init; }

            [JsonPropertyName("patchStarted")]
            public bool? PatchStarted { get; init; }

            [JsonPropertyName("timeline")]
            public TimelinePayload[] Timeline { get; init; } = Array.Empty<TimelinePayload>();

            [JsonPropertyName("approvalRequiredActions")]
            public ApprovalRequiredActionPayload[] ApprovalRequiredActions { get; init; } = Array.Empty<ApprovalRequiredActionPayload>();

            [JsonPropertyName("externalAttempts")]
            public int ExternalAttempts { get; init; }

            [JsonPropertyName("outsideBoundaryAttempts")]
            public int OutsideBoundaryAttempts { get; init; }

            [JsonPropertyName("highRiskApprovalRequiredActions")]
            public int HighRiskApprovalRequiredActions { get; init; }

            [JsonPropertyName("deniedActions")]
            public int DeniedActions { get; init; }

            [JsonPropertyName("blockedActions")]
            public int BlockedActions { get; init; }

            [JsonPropertyName("requestedActions")]
            public int RequestedActions { get; init; }

            [JsonPropertyName("executedActions")]
            public int ExecutedActions { get; init; }

            [JsonPropertyName("failedActions")]
            public int FailedActions { get; init; }

            [JsonPropertyName("hostBoundaryPreserved")]
            public bool HostBoundaryPreserved { get; init; }

            [JsonPropertyName("actionLifecycle")]
            public ActionLifecyclePayload[] ActionLifecycle { get; init; } = Array.Empty<ActionLifecyclePayload>();

            [JsonPropertyName("approvalStatusSummary")]
            public ApprovalStatusSummaryPayload ApprovalStatusSummary { get; init; } = new();
        }

        internal sealed class ApprovalStatusSummaryPayload
        {
            [JsonPropertyName("allowed")]
            public int Allowed { get; init; }

            [JsonPropertyName("approvalRequired")]
            public int ApprovalRequired { get; init; }

            [JsonPropertyName("denied")]
            public int Denied { get; init; }

            [JsonPropertyName("notApplicable")]
            public int NotApplicable { get; init; }
        }

        private sealed class SessionContinuationPayload
        {
            [JsonPropertyName("lastSuccessfulStep")]
            public string LastSuccessfulStep { get; init; } = string.Empty;
            [JsonPropertyName("lastKnownAction")]
            public string LastKnownAction { get; init; } = string.Empty;
        }

        internal sealed class ApprovalRequiredActionPayload
        {
            [JsonPropertyName("actionType")]
            public string ActionType { get; init; } = string.Empty;
            [JsonPropertyName("command")]
            public string Command { get; init; } = string.Empty;
            [JsonPropertyName("path")]
            public string Path { get; init; } = string.Empty;
            [JsonPropertyName("normalizedTarget")]
            public string NormalizedTarget { get; init; } = string.Empty;
            [JsonPropertyName("sandboxRoot")]
            public string SandboxRoot { get; init; } = string.Empty;
            [JsonPropertyName("projectRoot")]
            public string ProjectRoot { get; init; } = string.Empty;
            [JsonPropertyName("worktreeRoot")]
            public string WorktreeRoot { get; init; } = string.Empty;
            [JsonPropertyName("riskLevel")]
            public string RiskLevel { get; init; } = string.Empty;
            [JsonPropertyName("reasonCode")]
            public string ReasonCode { get; init; } = string.Empty;
            [JsonPropertyName("expectedEffect")]
            public string ExpectedEffect { get; init; } = string.Empty;
            [JsonPropertyName("reason")]
            public string Reason { get; init; } = string.Empty;
            [JsonPropertyName("approvalStatus")]
            public string ApprovalStatus { get; init; } = string.Empty;
            [JsonPropertyName("isInsideSandbox")]
            public bool IsInsideSandbox { get; init; }
        }

        internal sealed class ActionLifecyclePayload
        {
            [JsonPropertyName("sequence")]
            public int Sequence { get; init; }
            [JsonPropertyName("actionType")]
            public string ActionType { get; init; } = string.Empty;
            [JsonPropertyName("actionCorrelationId")]
            public string ActionCorrelationId { get; init; } = string.Empty;
            [JsonPropertyName("target")]
            public string Target { get; init; } = string.Empty;
            [JsonPropertyName("command")]
            public string Command { get; init; } = string.Empty;
            [JsonPropertyName("normalizedTarget")]
            public string NormalizedTarget { get; init; } = string.Empty;
            [JsonPropertyName("lifecycleState")]
            public string LifecycleState { get; init; } = string.Empty;
            [JsonPropertyName("reasonCode")]
            public string ReasonCode { get; init; } = string.Empty;
            [JsonPropertyName("reason")]
            public string Reason { get; init; } = string.Empty;
            [JsonPropertyName("approvalStatus")]
            public string ApprovalStatus { get; init; } = string.Empty;
            [JsonPropertyName("isInsideSandbox")]
            public bool IsInsideSandbox { get; init; }
        }

        private sealed class FailurePayload
        {
            public string RootCauseCode { get; init; } = string.Empty;
            public string FailedStage { get; init; } = string.Empty;
            public string LastSuccessfulStep { get; init; } = string.Empty;
            public string FailedStep { get; init; } = string.Empty;
            public string ReasonCode { get; init; } = string.Empty;
            public string Explanation { get; init; } = string.Empty;
            public string PipelineStoppedReason { get; init; } = string.Empty;
            public string DownstreamNotStarted { get; init; } = string.Empty;
            public string LoopStage { get; init; } = string.Empty;
            public int MaxIterations { get; init; }
            public int IterationsUsed { get; init; }
            public string LastKnownAction { get; init; } = string.Empty;
            public bool ModelCallStarted { get; init; }
            public bool PatchStarted { get; init; }
            public bool BuildStarted { get; init; }
            public TimelinePayload[] Timeline { get; init; } = Array.Empty<TimelinePayload>();
        }

        internal sealed class TimelinePayload
        {
            [JsonPropertyName("stage")]
            public string Stage { get; init; } = string.Empty;

            [JsonPropertyName("status")]
            public string Status { get; init; } = string.Empty;

            [JsonPropertyName("message")]
            public string Message { get; init; } = string.Empty;
        }

        private sealed class ChangedHintPayload
        {
            [JsonPropertyName("file")]
            public string File { get; init; } = string.Empty;

            [JsonPropertyName("hint")]
            public string Hint { get; init; } = string.Empty;
        }

        internal sealed class ChangedHint
        {
            public string File { get; init; } = string.Empty;
            public string Hint { get; init; } = string.Empty;
        }

        private sealed class ChangedRange
        {
            public string File { get; init; } = string.Empty;
            public int StartLine { get; init; }
            public int EndLine { get; init; }
        }

        private sealed class ChangedRangePayload
        {
            [JsonPropertyName("file")]
            public string File { get; init; } = string.Empty;

            [JsonPropertyName("startLine")]
            public int StartLine { get; init; }

            [JsonPropertyName("endLine")]
            public int EndLine { get; init; }
        }

        internal sealed class ChangedKind
        {
            public string File { get; init; } = string.Empty;
            public string Kind { get; init; } = string.Empty;
        }

        private sealed class ChangedKindPayload
        {
            [JsonPropertyName("file")]
            public string File { get; init; } = string.Empty;

            [JsonPropertyName("kind")]
            public string Kind { get; init; } = string.Empty;
        }

        internal sealed class StructuredDiagnostic
        {
            public string RootCause { get; init; } = string.Empty;
            public string AttemptedFix { get; init; } = string.Empty;
            public string WhyDenied { get; init; } = string.Empty;
            public string NextSafeAction { get; init; } = string.Empty;
        }

    }
#pragma warning restore CS0162
}


