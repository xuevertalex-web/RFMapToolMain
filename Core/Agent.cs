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
            var requestedNewFile = ExtractRequestedNewFilePath(task);
            tracer.LogActionEvent("TaskReceived", "Agent", ExecutionTracer.ActionLogLevel.Info, "received", metadata: new Dictionary<string, object?>
            {
                { "task", task }
            });
            tracer.LogActionEvent("TaskLifecycle", "Agent", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
            {
                { "task", task },
                { "requested_new_file", requestedNewFile ?? string.Empty }
            });

            if (IsSuspiciousInjectedToolTask(task))
            {
                var message = "Task contains raw tool syntax. Provide a normal natural-language task instead.";
                tracer.MarkStopPoint("Agent", "TASK_CONTAINS_TOOL_SYNTAX", message, new[] { "Indexing", "ModelRequest", "PatchApply", "BuildVerification" });
                return FinalizeRunResult(false, message, "Task rejected before execution", "TASK_CONTAINS_TOOL_SYNTAX", Array.Empty<string>(), Array.Empty<ChangedHint>(), Array.Empty<ChangedRange>(), Array.Empty<ChangedKind>(), false);
            }

            if (IsLowSignalTask(task))
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
                else if (!string.IsNullOrEmpty(indexResult.Error))
                {
                }

                var targetResolutionGate = new TargetResolutionGate(_projectIndexer, _contextBuilder.Tracer);
                var targetResolution = await targetResolutionGate.ResolveAsync(task);
                
                // Если целевое разрешение не удалось - просто продолжаем без ограничений, агент сам решит куда писать
                if (targetResolution.IsFailed)
                {
                    _memory.Add("target_resolution_gate", $"SKIPPED:{targetResolution.ReasonCode}:{targetResolution.Reason}");
                    // Оставляем targetResolution пустым, это нормально
                }

                var gatedTargetFiles = requestedNewFile != null
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
                var analysisOnlyTask = IsAnalysisOnlyTask(task);
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
                        ? BuildCompactAnalysisContext(contextInfo)
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
                        ? BuildAnalysisPromptWithContext(task, iteration, currentResponse, contextString)
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

                    var isHardFailure = runtimeResult?.IsFailure ?? IsHardLlmFailureResponse(currentResponse);
                    if (isHardFailure)
                    {
                        _memory.Add("llm_failure", currentResponse, "LlmUnavailableOrRequestFailed");
                        if (analysisOnlyTask)
                        {
                            var fallbackReason = ResolveFallbackReason(runtimeResult, currentResponse);
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
                            var fallbackSummary = BuildAnalysisFallbackSummary(task, contextInfo, fallbackReason);
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
                                provider: ResolveProviderName(_llmClient, runtimeMetadata),
                                model: ResolveModelName(_llmClient, runtimeMetadata),
                                degradedFlags: new[] { "ANALYSIS_FALLBACK_USED" },
                                fallbackReason: fallbackReason,
                                fallbackMode: "INDEXED_CONTEXT_SUMMARY",
                                payloadFinalStatus: "fallback-success",
                                timeline: BuildAnalysisTimeline(modelTimedOut: string.Equals(fallbackReason, "MODEL_TIMEOUT", StringComparison.Ordinal), fallbackUsed: true));
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
                            provider: ResolveProviderName(_llmClient, runtimeMetadata),
                            model: ResolveModelName(_llmClient, runtimeMetadata),
                            degradedFlags: Array.Empty<string>(),
                            fallbackReason: string.Empty,
                            fallbackMode: string.Empty,
                            payloadFinalStatus: "success",
                            timeline: BuildAnalysisTimeline(modelTimedOut: false, fallbackUsed: false));
                    }

                    // Check for tool calls
                    if (_toolCaller.ContainsToolCalls(currentResponse))
                    {
                        var toolCalls = _toolCaller.ParseToolCalls(currentResponse);
                        lastSuccessfulStep = "ToolCallsParsed";
                        lastKnownAction = $"Parsed {toolCalls.Count} tool calls";
                        if (toolCalls.Count == 0)
                        {
                            if (IsNonSubstantiveNoToolResponse(currentResponse))
                            {
                                currentResponse = "Your previous response did not contain the final analysis. Provide the final answer now. Do not say what you will do. Do not ask for more steps. Do not emit a tool call.";
                                continue;
                            }

                            if (!analysisOnlyTask && IsBroadEngineeringIntent(task))
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

                        var mutationIntentTask = IsMutationIntentTask(task) || requestedNewFile != null;

                        var mutationCall = toolCalls.FirstOrDefault(IsMutationLikeToolCall);
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

                        patchStarted = patchStarted || toolCalls.Any(IsMutationLikeToolCall);
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
                                    var filePath = ExtractWriteTargetPath(call.Input);
                                    if (!string.IsNullOrWhiteSpace(filePath))
                                    {
                                        changedFiles.Add(filePath);
                                        tracer.MarkChangedFile(filePath);
                                        var patchDecision = BuildPatchDecision(filePath, call.Input, resolvedFiles);
                                        _contextBuilder.Tracer.LogPatchDecision(patchDecision);
                                        changedHints[filePath] = BuildChangedHint(filePath, call.Input, patchDecision);
                                        var changedRange = BuildChangedRange(filePath, call.Input, patchDecision, _projectIndexer.SymbolDirectory);
                                        if (changedRange != null)
                                        {
                                            changedRanges[filePath] = changedRange;
                                        }
                                        changedKinds[filePath] = BuildChangedKind(task, call.Input, patchDecision, buildResult: null);
                                        
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
                        if (IsMutationIntentTask(task) || requestedNewFile != null)
                        {
                            currentResponse = requestedNewFile != null
                                ? $"This is a file creation task. Use the file tool now to write:{requestedNewFile}:... and create the requested file. Do not answer with explanation only."
                                : "This is a code change task. Use the file tool now to write Program.cs and make a concrete edit. Do not answer with code only.";
                            continue;
                        }

                        if (IsNonSubstantiveNoToolResponse(currentResponse))
                        {
                            currentResponse = "Your previous response did not contain the final analysis. Provide the final answer now. Do not say what you will do. Do not ask for more steps. Do not emit a tool call.";
                            continue;
                        }

                        _memory.Add("final_response", currentResponse);
                        if (IsBroadEngineeringIntent(task))
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
                        Timeline = BuildMaxIterationsTimeline(actualIterationsUsed, lastSuccessfulStep, lastKnownAction)
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

        /// <summary>
        /// Log file state status and symbol information for debugging.
        /// Shows which files are Hot, Dirty, or Clean and their extracted symbols.
        /// </summary>
        private void LogFileStateStatus(ContextInformation contextInfo)
        {
            if (contextInfo.SelectedFiles.Count == 0)
                return;

            Console.WriteLine("\n📊 Context Information:");
            foreach (var file in contextInfo.SelectedFiles)
            {
                var stateLabel = "";
                if (contextInfo.FileStateFlags.TryGetValue(file, out var flag))
                {
                    stateLabel = $" {flag}";
                }
                else
                {
                    stateLabel = " (Clean)";
                }

                var lastActivityUtc = _fileStateManager.GetLastActivityUtc(file);
                var recencyLabel = lastActivityUtc == DateTime.MinValue
                    ? ""
                    : $" | LastActivityUtc: {lastActivityUtc:O}";
                
                // Get symbols for this file if available
                var symbols = _projectIndexer.SymbolDirectory.GetSymbols(file);
                var symbolsLabel = symbols.Count > 0 ? $" | Symbols: {string.Join(", ", symbols.Take(5))}" + (symbols.Count > 5 ? $"... +{symbols.Count - 5} more" : "") : "";
                
                Console.WriteLine($"  • {file}{stateLabel}{symbolsLabel}{recencyLabel}");
            }
            Console.WriteLine();
        }

        private static ExecutionTracer.PatchDecision BuildPatchDecision(string filePath, string input, List<string> alternativeFiles)
        {
            var scope = input.Length > 220 ? "minimal-slice" : "targeted-write";
            var riskLevel = alternativeFiles.Count > 5 ? "medium" : "low";

            return new ExecutionTracer.PatchDecision
            {
                Timestamp = DateTime.UtcNow,
                TargetFile = filePath,
                TargetMethod = string.Empty,
                Scope = scope,
                Reason = "File tool write command selected for minimal patch application",
                RiskLevel = riskLevel,
                AlternativeFiles = alternativeFiles.Take(5).ToList()
            };
        }

        private static string ExtractWriteTargetPath(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || !input.StartsWith("write:", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var payload = input.Substring(6);
            if (string.IsNullOrWhiteSpace(payload))
                return string.Empty;

            // Handle Windows absolute paths like C:\foo\bar.cs:<content>.
            if (payload.Length >= 3 &&
                payload[1] == ':' &&
                (payload[2] == '\\' || payload[2] == '/'))
            {
                var separator = payload.IndexOf(':', 3);
                return separator >= 0 ? payload[..separator].Trim() : payload.Trim();
            }

            var idx = payload.IndexOf(':');
            return idx >= 0 ? payload[..idx].Trim() : payload.Trim();
        }

        private static ChangedHint BuildChangedHint(string filePath, string toolInput, ExecutionTracer.PatchDecision patchDecision)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var reasonHint = NormalizeHint(patchDecision.Reason);
            if (!string.IsNullOrWhiteSpace(reasonHint))
            {
                return new ChangedHint { File = filePath, Hint = reasonHint };
            }

            var actionHint = ExtractActionHint(toolInput, fileName);
            if (!string.IsNullOrWhiteSpace(actionHint))
            {
                return new ChangedHint { File = filePath, Hint = actionHint };
            }

            return new ChangedHint { File = filePath, Hint = "Updated by agent" };
        }

        private static ChangedKind BuildChangedKind(string task, string toolInput, ExecutionTracer.PatchDecision patchDecision, Execution.BuildVerifier.BuildResult? buildResult)
        {
            var intent = ClassifyIntent(task, toolInput, patchDecision.Reason, buildResult);
            return new ChangedKind
            {
                File = patchDecision.TargetFile,
                Kind = intent.ToString()
            };
        }

        private enum ChangedKindType
        {
            BugFix,
            Validation,
            Refactor,
            BuildFix,
            FeatureAdd,
            Update,
            Unknown
        }

        private static ChangedKindType ClassifyIntent(string task, string toolInput, string patchReason, Execution.BuildVerifier.BuildResult? buildResult)
        {
            var combined = string.Join(" ", new[] { task, toolInput, patchReason, buildResult?.Errors != null ? string.Join(" ", buildResult.Errors) : string.Empty })
                .ToLowerInvariant();

            if (combined.Contains("validation") || combined.Contains("null check") || combined.Contains("input check"))
                return ChangedKindType.Validation;
            if (combined.Contains("refactor") || combined.Contains("refined") || combined.Contains("rework"))
                return ChangedKindType.Refactor;
            if (combined.Contains("build error") || combined.Contains("compile") || combined.Contains("build failed") || combined.Contains("cs") || combined.Contains("restore"))
                return ChangedKindType.BuildFix;
            if (combined.Contains("fix") || combined.Contains("bug") || combined.Contains("error handling") || combined.Contains("exception"))
                return ChangedKindType.BugFix;
            if (combined.Contains("add") || combined.Contains("new") || combined.Contains("feature") || combined.Contains("implement"))
                return ChangedKindType.FeatureAdd;
            if (combined.Contains("update") || combined.Contains("adjust") || combined.Contains("change") || combined.Contains("modify"))
                return ChangedKindType.Update;

            return ChangedKindType.Unknown;
        }

        private static bool IsMutationLikeToolCall(ToolCaller.ToolCall call)
        {
            var toolName = call?.ToolName ?? string.Empty;
            var input = call?.Input ?? string.Empty;

            if (!toolName.Equals("file", StringComparison.OrdinalIgnoreCase))
                return false;

            return input.StartsWith("write:", StringComparison.OrdinalIgnoreCase) ||
                   input.StartsWith("patch:", StringComparison.OrdinalIgnoreCase) ||
                   input.StartsWith("edit:", StringComparison.OrdinalIgnoreCase) ||
                   input.StartsWith("change:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAnalysisOnlyTask(string task)
        {
            if (string.IsNullOrWhiteSpace(task))
                return false;

            var normalized = task.ToLowerInvariant();
            if (normalized.Contains("\u043e\u043f\u0438\u0448\u0438") ||
                normalized.Contains("\u043e\u043f\u0438\u0441\u0430\u0442\u044c") ||
                normalized.Contains("\u043e\u0431\u044a\u044f\u0441\u043d\u0438") ||
                normalized.Contains("\u043e\u0431\u044a\u044f\u0441\u043d\u0438\u0442\u044c") ||
                normalized.Contains("\u043e\u0431\u0437\u043e\u0440") ||
                normalized.Contains("\u0441\u0442\u0440\u0443\u043a\u0442\u0443\u0440\u0443") ||
                normalized.Contains("\u0441\u0442\u0440\u0443\u043a\u0442\u0443\u0440\u0430") ||
                normalized.Contains("\u043a\u043b\u044e\u0447\u0435\u0432\u044b\u0435 \u0444\u0430\u0439\u043b\u044b") ||
                normalized.Contains("\u0440\u0430\u0441\u0441\u043a\u0430\u0436\u0438"))
            {
                return true;
            }

            return normalized.Contains("analyze") ||
                   normalized.Contains("analyse") ||
                   normalized.Contains("summarize") ||
                   normalized.Contains("summarise") ||
                   normalized.Contains("explain") ||
                   normalized.Contains("review") ||
                   normalized.Contains("diagnose") ||
                   normalized.Contains("опиши") ||
                   normalized.Contains("описать") ||
                   normalized.Contains("объясни") ||
                   normalized.Contains("объяснить") ||
                   normalized.Contains("обзор") ||
                   normalized.Contains("структуру") ||
                   normalized.Contains("структура") ||
                   normalized.Contains("ключевые файлы") ||
                   normalized.Contains("расскажи");
        }

        private static bool IsSuspiciousInjectedToolTask(string task)
        {
            if (string.IsNullOrWhiteSpace(task))
                return false;

            var normalized = task.Trim();
            return normalized.Contains("TOOL:", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("INPUT:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLowSignalTask(string task)
        {
            if (string.IsNullOrWhiteSpace(task))
                return true;

            var normalized = task.Trim();
            if (Path.IsPathRooted(normalized) && !normalized.Contains(' '))
                return true;

            var signalChars = normalized.Count(char.IsLetterOrDigit);
            if (signalChars < 3)
                return true;

            if (normalized.Length >= 256)
            {
                var signalRatio = (double)signalChars / normalized.Length;
                var substantiveTokenCount = Regex.Matches(normalized, @"[\p{L}\p{Nd}_]{3,}").Count;
                if (signalRatio < 0.35 || substantiveTokenCount < 3)
                    return true;
            }

            return false;
        }

        private static bool IsNonSubstantiveNoToolResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return true;

            var normalized = response.Trim();
            if (normalized.Length < 32)
                return true;

            var lower = normalized.ToLowerInvariant();
            return lower.StartsWith("i will analyze", StringComparison.Ordinal) ||
                   lower.StartsWith("i will review", StringComparison.Ordinal) ||
                   lower.StartsWith("i will examine", StringComparison.Ordinal) ||
                   lower.StartsWith("i will inspect", StringComparison.Ordinal) ||
                   lower.StartsWith("i will look for", StringComparison.Ordinal) ||
                   lower.StartsWith("i'll analyze", StringComparison.Ordinal) ||
                   lower.StartsWith("i'll review", StringComparison.Ordinal) ||
                   lower.StartsWith("i'll examine", StringComparison.Ordinal) ||
                   lower.StartsWith("i'll inspect", StringComparison.Ordinal) ||
                   lower.StartsWith("\u044f \u043f\u0440\u043e\u0430\u043d\u0430\u043b\u0438\u0437\u0438\u0440\u0443\u044e", StringComparison.Ordinal) ||
                   lower.StartsWith("\u044f \u043f\u0440\u043e\u0432\u0435\u0440\u044e", StringComparison.Ordinal) ||
                   lower.StartsWith("\u044f \u043d\u0430\u0439\u0434\u0443", StringComparison.Ordinal) ||
                   lower.StartsWith("\u044f \u0440\u0430\u0441\u0441\u043c\u043e\u0442\u0440\u044e", StringComparison.Ordinal);
        }

        private static string BuildAnalysisFallbackSummary(string task, ContextInformation contextInfo, string fallbackReason)
        {
            var files = contextInfo.SelectedFiles.Take(10).ToList();
            var lines = new List<string> { "Краткий обзор проекта:" };
            if (files.Count == 0)
            {
                lines.Add("- Подходящие файлы не были выбраны из контекста.");
                lines.Add("- LLM недоступна, поэтому обзор ограничен индексированием проекта.");
                return string.Join(Environment.NewLine, lines);
            }

            lines.Add($"- По задаче выбрано {files.Count} ключевых файлов контекста.");

            var topFolders = files
                .Select(file => file.Contains(Path.DirectorySeparatorChar) || file.Contains(Path.AltDirectorySeparatorChar)
                    ? file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0]
                    : "(root)")
                .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .Select(g => $"{g.Key}: {g.Count()}")
                .ToList();

            if (topFolders.Count > 0)
                lines.Add($"- Основные области: {string.Join(", ", topFolders)}.");

            foreach (var file in files)
            {
                var symbols = contextInfo.RelevantSymbols.TryGetValue(file, out var relevantSymbols)
                    ? relevantSymbols.Take(5).ToList()
                    : new List<string>();
                var symbolSuffix = symbols.Count > 0
                    ? $" | Символы: {string.Join(", ", symbols)}"
                    : string.Empty;
                lines.Add($"- {file}{symbolSuffix}");
            }

            lines.Add(GetAnalysisFallbackReasonText(fallbackReason));
            return string.Join(Environment.NewLine, lines);
        }

        private static string GetAnalysisFallbackReasonText(string fallbackReason)
        {
            if (string.Equals(fallbackReason, "MODEL_TIMEOUT", StringComparison.OrdinalIgnoreCase))
                return "- Ответ собран из индексированного контекста, потому что локальная модель не завершила запрос вовремя.";

            if (string.Equals(fallbackReason, "PROVIDER_UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
                return "- Ответ собран из индексированного контекста, потому что локальная модель недоступна или не найдена.";

            return "- Ответ собран из индексированного контекста, потому что запрос к локальной модели завершился ошибкой.";
        }

        private static string BuildCompactAnalysisContext(ContextInformation contextInfo)
        {
            if (contextInfo.SelectedFiles.Count == 0)
                return "No indexed files were selected for this analysis task.";

            var lines = new List<string>();
            foreach (var file in contextInfo.SelectedFiles.Take(4))
            {
                lines.Add($"FILE: {file}");
                if (contextInfo.RelevantSymbols.TryGetValue(file, out var symbols) && symbols.Count > 0)
                    lines.Add($"SYMBOLS: {string.Join(", ", symbols.Take(6))}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static bool IsMutationIntentTask(string task)
        {
            if (string.IsNullOrWhiteSpace(task))
                return false;

            var normalized = task.ToLowerInvariant();
            return normalized.Contains("fix") ||
                   normalized.Contains("create") ||
                   normalized.Contains("generate") ||
                   normalized.Contains("write") ||
                   normalized.Contains("make") ||
                   normalized.Contains("change") ||
                   normalized.Contains("update") ||
                   normalized.Contains("add") ||
                   normalized.Contains("implement") ||
                   normalized.Contains("modify") ||
                   normalized.Contains("refactor") ||
                   normalized.Contains("создай") ||
                   normalized.Contains("создать") ||
                   normalized.Contains("добавь") ||
                   normalized.Contains("добавить") ||
                   normalized.Contains("измени") ||
                   normalized.Contains("исправь") ||
                   normalized.Contains("напиши") ||
                   normalized.Contains("сделай") ||
                   normalized.Contains("файл") ||
                   normalized.Contains("класс") ||
                   normalized.Contains("ensure build passes");
        }

        private static string? ExtractRequestedNewFilePath(string task)
        {
            if (string.IsNullOrWhiteSpace(task))
                return null;

            var normalized = task.ToLowerInvariant();
            var isCreateIntent = normalized.Contains("create") ||
                                 normalized.Contains("generate") ||
                                 normalized.Contains("write") ||
                                 normalized.Contains("make") ||
                                 normalized.Contains("создай") ||
                                 normalized.Contains("создать") ||
                                 normalized.Contains("добавь") ||
                                 normalized.Contains("добавить") ||
                                 normalized.Contains("напиши");

            if (!isCreateIntent)
                return null;

            var fileMatch = Regex.Match(task, @"([A-Za-z0-9_\-./\\]+\.cs)\b");
            if (!fileMatch.Success)
                return null;

            var candidate = fileMatch.Groups[1].Value.Trim();
            return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
        }

        private static bool IsHardLlmFailureResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return false;

            var fallbackMetadata = new LlmProviderMetadata("legacy", string.Empty, "legacy-classifier");
            var fallbackProfile = LlmProfiles.Resolve("ollama");
            var classified = LlmRuntimeClassifier.Classify(response, fallbackMetadata, fallbackProfile, LlmRuntimePolicy.Default);
            return classified.IsFailure;
        }

        private bool TryRepairCs8802(BuildVerifier.BuildResult buildResult, HashSet<string> changedFiles, out string? nextPrompt)
        {
            nextPrompt = null;
            var cs8802 = buildResult.Errors.FirstOrDefault(e => e.Contains("CS8802", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(cs8802) || _sessionContext == null)
                return false;

            var programPath = Path.Combine(_sessionContext.ActiveWorkspaceRoot, "Program.cs");
            if (!File.Exists(programPath) || !ContainsTopLevelStatements(programPath))
                return false;

            foreach (var changedFile in changedFiles.Where(f =>
                         f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                         !Path.GetFileName(f).Equals("Program.cs", StringComparison.OrdinalIgnoreCase)))
            {
                if (!File.Exists(changedFile))
                    continue;

                var fileText = File.ReadAllText(changedFile);
                if (!ContainsMainEntryPoint(fileText))
                    continue;

                var normalized = NormalizeHelperClassWithoutMain(fileText);
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

        private static bool ContainsTopLevelStatements(string programPath)
        {
            var text = File.ReadAllText(programPath);
            return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                .Select(line => line.Trim())
                .Any(line => !string.IsNullOrWhiteSpace(line) &&
                             !line.StartsWith("using ", StringComparison.Ordinal) &&
                             !line.StartsWith("namespace ", StringComparison.Ordinal) &&
                             !line.StartsWith("//", StringComparison.Ordinal) &&
                             !line.StartsWith("/*", StringComparison.Ordinal) &&
                             line != "{" &&
                             line != "}" &&
                             !line.StartsWith("[", StringComparison.Ordinal));
        }

        private static bool ContainsMainEntryPoint(string text) =>
            Regex.IsMatch(text, @"\bstatic\s+(?:async\s+)?(?:void|int|Task(?:<int>)?)\s+Main\s*\(", RegexOptions.Multiline);

        private static string NormalizeHelperClassWithoutMain(string text)
        {
            var withoutMain = Regex.Replace(
                text,
                @"^\s*(?:public|private|protected|internal)?\s*static\s+(?:async\s+)?(?:void|int|Task(?:<int>)?)\s+Main\s*\([^)]*\)\s*\{[\s\S]*?^\s*\}",
                string.Empty,
                RegexOptions.Multiline);

            return withoutMain.Trim() + Environment.NewLine;
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
            return EmitStructuredDiagnosticResult(diagnostic, changedFiles, changedHints, changedRanges, changedKinds);
        }

        private static string EmitStructuredDiagnosticResult(StructuredDiagnostic diagnostic, IEnumerable<string> changedFiles, IEnumerable<ChangedHint> changedHints, IEnumerable<ChangedRange> changedRanges, IEnumerable<ChangedKind> changedKinds)
        {
            var message = $@"Structured diagnostic:
root_cause: {diagnostic.RootCause}
attempted_fix: {diagnostic.AttemptedFix}
why_denied: {diagnostic.WhyDenied}
next_safe_action: {diagnostic.NextSafeAction}";

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

        private static TimelinePayload[] BuildMaxIterationsTimeline(int iterationsUsed, string lastSuccessfulStep, string lastKnownAction)
        {
            var events = new List<TimelinePayload>
            {
                new()
                {
                    Stage = "IterationLoopStarted",
                    Status = "started",
                    Message = $"AgentIterationLoop started with max iterations {MAX_ITERATIONS}"
                }
            };

            for (var iteration = 1; iteration <= iterationsUsed; iteration++)
            {
                events.Add(new TimelinePayload
                {
                    Stage = "IterationStarted",
                    Status = "started",
                    Message = $"Iteration {iteration}/{MAX_ITERATIONS} started"
                });
                events.Add(new TimelinePayload
                {
                    Stage = "IterationCompleted",
                    Status = "completed",
                    Message = iteration == iterationsUsed
                        ? $"{lastSuccessfulStep}: {lastKnownAction}"
                        : $"Iteration {iteration}/{MAX_ITERATIONS} completed"
                });
            }

            events.Add(new TimelinePayload
            {
                Stage = "MaxIterationsReached",
                Status = "failed",
                Message = $"Iteration budget exhausted ({iterationsUsed}/{MAX_ITERATIONS})"
            });
            events.Add(new TimelinePayload
            {
                Stage = "RunFailedWithRootCause",
                Status = "failed",
                Message = "MAX_ITERATIONS_REACHED"
            });

            return events.ToArray();
        }

        private static TimelinePayload[] BuildAnalysisTimeline(bool modelTimedOut, bool fallbackUsed)
        {
            var events = new List<TimelinePayload>
            {
                new()
                {
                    Stage = "TaskReceived",
                    Status = "received",
                    Message = "Task accepted for analysis"
                },
                new()
                {
                    Stage = "IndexingStarted",
                    Status = "started",
                    Message = "Project indexing started"
                },
                new()
                {
                    Stage = "IndexingCompleted",
                    Status = "completed",
                    Message = "Project indexing completed"
                },
                new()
                {
                    Stage = "ModelCallStarted",
                    Status = "started",
                    Message = "Model call started"
                }
            };

            if (modelTimedOut)
            {
                events.Add(new TimelinePayload
                {
                    Stage = "ModelCallTimedOut",
                    Status = "timed_out",
                    Message = "Model call timed out"
                });
            }

            if (fallbackUsed)
            {
                events.Add(new TimelinePayload
                {
                    Stage = "AnalysisFallbackStarted",
                    Status = "started",
                    Message = "Using indexed context fallback"
                });
                events.Add(new TimelinePayload
                {
                    Stage = "AnalysisFallbackCompleted",
                    Status = "completed",
                    Message = "Indexed context summary prepared"
                });
            }

            events.Add(new TimelinePayload
            {
                Stage = "RunCompleted",
                Status = "completed",
                Message = fallbackUsed ? "Run completed via fallback" : "Run completed"
            });

            return events.ToArray();
        }

        private static bool IsModelTimeoutResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return false;

            return response.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                   response.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveFallbackReason(LlmRuntimeResult? runtimeResult, string response)
        {
            if (runtimeResult is not null)
            {
                return runtimeResult.Status switch
                {
                    LlmRuntimeStatus.ModelTimeout => "MODEL_TIMEOUT",
                    LlmRuntimeStatus.ProviderUnavailable => "PROVIDER_UNAVAILABLE",
                    LlmRuntimeStatus.UnsupportedCapability => "UNSUPPORTED_CAPABILITY",
                    _ => "LLM_REQUEST_FAILED"
                };
            }

            return IsModelTimeoutResponse(response) ? "MODEL_TIMEOUT" : "LLM_REQUEST_FAILED";
        }

        private static string ResolveProviderName(ILLMClient llmClient, LlmProviderMetadata? metadata = null)
        {
            if (metadata is not null && !string.IsNullOrWhiteSpace(metadata.Provider))
                return metadata.Provider;

            if (llmClient == null)
                return string.Empty;

            var typeName = llmClient.GetType().Name;
            if (typeName.EndsWith("Client", StringComparison.OrdinalIgnoreCase))
                typeName = typeName[..^"Client".Length];
            return typeName;
        }

        private static string ResolveModelName(ILLMClient llmClient, LlmProviderMetadata? metadata = null)
        {
            if (metadata is not null && !string.IsNullOrWhiteSpace(metadata.Model))
                return metadata.Model;

            if (llmClient == null)
                return string.Empty;

            var field = llmClient.GetType().GetField("_model", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field?.GetValue(llmClient) is string model && !string.IsNullOrWhiteSpace(model))
                return model;

            return string.Empty;
        }

        private static ChangedRange? BuildChangedRange(string filePath, string toolInput, ExecutionTracer.PatchDecision patchDecision, ProjectSymbolDirectory? symbolDirectory)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0)
            {
                return null;
            }

            var candidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(patchDecision.TargetMethod))
                candidates.Add(patchDecision.TargetMethod);

            var targetSymbol = ExtractTargetSymbol(toolInput);
            if (!string.IsNullOrWhiteSpace(targetSymbol))
                candidates.Add(targetSymbol);

            var fallbackName = Path.GetFileNameWithoutExtension(filePath);
            if (!string.IsNullOrWhiteSpace(fallbackName))
                candidates.Add(fallbackName);

            var indexedSymbols = symbolDirectory?.GetSymbols(filePath) ?? new List<string>();

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var symbolRange = FindBestSymbolRangeForFile(lines, indexedSymbols, candidate);
                if (symbolRange is not null)
                {
                    return new ChangedRange
                    {
                        File = filePath,
                        StartLine = symbolRange.Value.startLine,
                        EndLine = symbolRange.Value.endLine
                    };
                }
            }

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
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
            if (string.IsNullOrWhiteSpace(candidate) || lines.Length == 0)
                return null;

            var searchOrder = indexedSymbols
                .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!searchOrder.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                searchOrder.Insert(0, candidate);

            foreach (var symbol in searchOrder)
            {
                var symbolLine = FindSymbolDeclarationLine(lines, symbol);
                if (symbolLine < 0)
                    continue;

                var methodStart = FindNearestDeclarationStart(lines, symbolLine, "method");
                if (methodStart >= 0)
                {
                    var methodEnd = FindMatchingBlockEnd(lines, methodStart);
                    return (methodStart + 1, Math.Max(methodStart + 1, methodEnd + 1));
                }

                var classStart = FindNearestDeclarationStart(lines, symbolLine, "class");
                if (classStart >= 0)
                {
                    var classEnd = FindMatchingBlockEnd(lines, classStart);
                    return (classStart + 1, Math.Max(classStart + 1, classEnd + 1));
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

            var methodStart = FindNearestDeclarationStart(lines, anchorLineIndex, "method");
            if (methodStart >= 0)
            {
                var methodEnd = FindMatchingBlockEnd(lines, methodStart);
                return (methodStart + 1, methodEnd > methodStart ? methodEnd + 1 : methodStart + 1);
            }

            var classStart = FindNearestDeclarationStart(lines, anchorLineIndex, "class");
            if (classStart >= 0)
            {
                var classEnd = FindMatchingBlockEnd(lines, classStart);
                return (classStart + 1, classEnd > classStart ? classEnd + 1 : classStart + 1);
            }

            return null;
        }

        private static int FindNearestDeclarationStart(string[] lines, int anchorLineIndex, string declarationKind)
        {
            for (var i = anchorLineIndex; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (declarationKind.Equals("method", StringComparison.OrdinalIgnoreCase) && LooksLikeMethodDeclaration(line))
                    return i;
                if (declarationKind.Equals("class", StringComparison.OrdinalIgnoreCase) && LooksLikeClassDeclaration(line))
                    return i;
            }

            return -1;
        }

        private static bool LooksLikeMethodDeclaration(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            return Regex.IsMatch(line, @"(?:public|private|protected|internal)(?:\s+static)?(?:\s+async)?\s+(?:virtual\s+)?(?:override\s+)?[\w<>\.\[\],]+\s+\w+\s*\(");
        }

        private static bool LooksLikeClassDeclaration(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            return Regex.IsMatch(line, @"(?:public|private|protected|internal)?\s*(?:abstract|sealed|static)?\s*(?:partial\s+)?class\s+\w+");
        }

        private static int FindMatchingBlockEnd(string[] lines, int startLineIndex)
        {
            var braceDepth = 0;
            var seenOpeningBrace = false;

            for (var i = startLineIndex; i < lines.Length; i++)
            {
                var line = lines[i];
                for (var j = 0; j < line.Length; j++)
                {
                    var ch = line[j];
                    if (ch == '{')
                    {
                        braceDepth++;
                        seenOpeningBrace = true;
                    }
                    else if (ch == '}')
                    {
                        braceDepth--;
                        if (seenOpeningBrace && braceDepth <= 0)
                            return i;
                    }
                }
            }

            return startLineIndex;
        }

        private static int FindSymbolDeclarationLine(string[] lines, string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return -1;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (LooksLikeMethodDeclaration(line) && line.Contains(symbol, StringComparison.Ordinal))
                    return i;

                if (LooksLikeClassDeclaration(line) && line.Contains(symbol, StringComparison.Ordinal))
                    return i;
            }

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(symbol, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }

            return -1;
        }

        private static int FindMatchingLine(string[] lines, string needle)
        {
            if (string.IsNullOrWhiteSpace(needle))
                return -1;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }

            return -1;
        }

        private static string ExtractActionHint(string toolInput, string fallbackSymbol)
        {
            var lower = toolInput.ToLowerInvariant();
            var symbol = ExtractTargetSymbol(toolInput) ?? fallbackSymbol;

            if (lower.Contains("validation"))
                return $"Added validation in {symbol}";
            if (lower.Contains("null"))
                return $"Added null check in {symbol}";
            if (lower.Contains("workspace") && lower.Contains("path"))
                return "Adjusted workspace path resolution";
            if (lower.Contains("build") && lower.Contains("error"))
                return $"Updated build error handling in {symbol}";
            if (lower.Contains("error handling"))
                return $"Updated error handling in {symbol}";
            if (lower.Contains("fix"))
                return $"Fixed {symbol}";
            if (lower.Contains("refactor"))
                return $"Refined {symbol}";

            return string.Empty;
        }

        private static string? ExtractTargetSymbol(string text)
        {
            var patterns = new[]
            {
                @"\bmethod\s+([A-Za-z_][A-Za-z0-9_]*)\b",
                @"\bclass\s+([A-Za-z_][A-Za-z0-9_]*)\b",
                @"\bfunction\s+([A-Za-z_][A-Za-z0-9_]*)\b",
                @"\bservice\s+([A-Za-z_][A-Za-z0-9_]*)\b",
                @"\b([A-Za-z_][A-Za-z0-9_]*)\s+method\b"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        private static string NormalizeHint(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var cleaned = Regex.Replace(text, @"\s+", " ").Trim();
            var firstSentence = cleaned.Split(new[] { '.', '!', '?' }, 2, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            if (firstSentence.Length > 120)
                firstSentence = firstSentence.Substring(0, 117).TrimEnd() + "...";
            return firstSentence;
        }

        private string BuildPrompt(string task, int iteration, string previousResponse)
        {
            var context = _memory.GetContextString(CONTEXT_WINDOW);
            var toolsDescription = _toolRegistry.GetToolsDescription();
            var responseLanguageRule = BuildResponseLanguageRule(task);

            var prompt = $@"You are a skilled C# coding agent with semantic understanding. Your task is to help with the following:

{task}

IMPORTANT GUIDELINES:
{responseLanguageRule}
- Modify ONLY the necessary parts of the code
- Keep the overall structure intact
- Make targeted, minimal changes
- Focus on correctness and compilation

Available tools:
{toolsDescription}

When you need to perform an action, use this format:
TOOL: tool_name
INPUT: command_here

Examples:
TOOL: file
INPUT: read:Program.cs

TOOL: file
INPUT: write:MyClass.cs:using System;

public class MyClass 
{{
    public void MyMethod() {{ }}
}}

Only use ONE tool call per iteration. After using a tool, wait for the result before proceeding.

{(iteration > 0 && !string.IsNullOrWhiteSpace(previousResponse) ? $"Previous iteration result:\n{previousResponse}\n" : "")}

{(context.Length > 0 ? $"Recent execution history:\n{context}\n" : "")}

What is your next step? Use tools to complete the task.";

            return prompt;
        }

        private string BuildPromptWithContext(string task, int iteration, string previousResponse, string codeContext, string regressionAdvice, string promptShapingAdvice, string strategyBiasAdvice)
        {
            var executionContext = _memory.GetContextString(CONTEXT_WINDOW);
            var taskProfile = _memorySystem.GetTaskProfileSummary(task);
            var toolsDescription = _toolRegistry.GetToolsDescription();
            var policyBlock = BuildPolicyBlock();
            var startupStateBlock = BuildStartupStateBlock();
            var responseLanguageRule = BuildResponseLanguageRule(task);

            var prompt = $@"You are a skilled C# coding agent with semantic understanding.

TASK:
{task}

IMPORTANT GUIDELINES:
{responseLanguageRule}
- Create new files directly in the workspace when the task asks for something new.
- Creating new folders and files inside the workspace is allowed.
- Do not ask the user for a path; choose an appropriate file name yourself.
- If the user asks for a simple calculator without specifying a path, create Calculator.cs in the workspace root.
- Modify ONLY necessary code
- Keep structure intact
- Make targeted, minimal changes
- Never rewrite entire files blindly
- Focus on correctness and compilation
- If the task is analysis-only, explanation-only, or diagnosis-only, answer directly without any tool call
- If you need to use a tool, use only one tool call per iteration
- Never invent new tool names or tool modes
- The only valid tool names are exactly the names listed below

RELEVANT CODE:
{codeContext}

{policyBlock}

{startupStateBlock}

Available tools:
{toolsDescription}

TOOL FORMAT:
TOOL: tool_name
INPUT: command_here

TOOL USAGE RULES:
- Use TOOL: file for read/write/delete/rename/move operations
- Use TOOL: build for build or verification operations
- For build, pass the workspace root or a path inside the workspace; do not pass a solution file as the working directory
- Do not emit any other TOOL name
- Do not emit multiple tool calls in one response
- Do not wrap tool calls in markdown fences

Examples:
TOOL: file
INPUT: read:MyClass.cs

TOOL: file
INPUT: write:MyClass.cs:using System;

public class MyClass {{ /* implementation */ }}

Only use ONE tool call per iteration. If you are not sure which tool to use, prefer a plain natural-language response instead of guessing a new tool name.

{(iteration > 0 && !string.IsNullOrWhiteSpace(previousResponse) ? $"Previous result:\n{previousResponse}\n" : string.Empty)}

{(string.IsNullOrWhiteSpace(taskProfile) ? string.Empty : $"Task profile:\n{taskProfile}\n")}

{(string.IsNullOrWhiteSpace(regressionAdvice) ? string.Empty : $"{regressionAdvice}\n")}

{(string.IsNullOrWhiteSpace(promptShapingAdvice) ? string.Empty : $"{promptShapingAdvice}\n")}

{(string.IsNullOrWhiteSpace(strategyBiasAdvice) ? string.Empty : $"{strategyBiasAdvice}\n")}

{(executionContext.Length > 0 ? $"Execution history:\n{executionContext}\n" : string.Empty)}

What is your next step?";

            return prompt;
        }

        private static string BuildAnalysisPromptWithContext(string task, int iteration, string previousResponse, string compactContext)
        {
            var responseLanguageRule = BuildResponseLanguageRule(task);

            return $@"You are a C# project analysis agent.

TASK:
{task}

RULES:
{responseLanguageRule}
- This is an analysis-only task.
- Do not use any tool.
- Do not ask for more files.
- Do not propose tool calls.
- Answer directly in concise natural language.
- Use only the provided indexed context.
- If the context is partial, explicitly say that the answer is based on indexed key files.

INDEXED PROJECT CONTEXT:
{compactContext}

{(iteration > 0 && !string.IsNullOrWhiteSpace(previousResponse) ? $"Previous result:\n{previousResponse}\n" : string.Empty)}

Write the final project overview now.";
        }

        private static string BuildResponseLanguageRule(string task)
        {
            return ContainsCyrillic(task)
                ? "- Answer in Russian. The user wrote in Russian, so the final response must be in Russian."
                : "- Answer in the same language as the user's task.";
        }

        private static bool ContainsCyrillic(string value)
        {
            return !string.IsNullOrEmpty(value) && value.Any(ch => ch >= '\u0400' && ch <= '\u04FF');
        }

        private string BuildPolicyBlock()
        {
            if (_sessionContext is null)
                return string.Empty;

            var mode = _sessionContext.AccessMode.ToString();
            return $@"WORKSPACE POLICY:
- Active workspace root: {_sessionContext.ActiveWorkspaceRoot}
- Runtime root: {_sessionContext.RuntimeRoot}
- Access mode: {mode}
- ReadOnly: read/analysis only; no write, delete, rename, move.
- WorkspaceWrite: patch/write/create allowed inside workspace; destructive ops denied.
- WorkspaceFullAccess: destructive ops allowed only inside workspace; runtime/protected paths remain denied.
- Never target the runtime root or protected paths.";
        }

        private string BuildStartupStateBlock()
        {
            if (_sessionContext is null && _workspaceResolution is null)
                return string.Empty;

            var lines = new List<string>();

            if (_workspaceResolution is not null)
            {
                lines.Add("WORKSPACE RESOLUTION:");
                lines.Add($"- Success: {_workspaceResolution.Success}");
                lines.Add($"- Reason: {_workspaceResolution.ReasonCodeName} / {_workspaceResolution.ReasonCode}");
                lines.Add($"- Source: {_workspaceResolution.Source ?? string.Empty}");
                lines.Add($"- Workspace root: {_workspaceResolution.WorkspaceRoot ?? string.Empty}");
                lines.Add($"- Message: {_workspaceResolution.Message}");
            }

            if (_sessionContext is not null)
            {
                if (lines.Count > 0)
                    lines.Add(string.Empty);

                lines.Add("SESSION STARTUP:");
                lines.Add($"- Session id: {_sessionContext.SessionId}");
                lines.Add($"- Runtime root: {_sessionContext.RuntimeRoot}");
                lines.Add($"- Active workspace root: {_sessionContext.ActiveWorkspaceRoot}");
                lines.Add($"- Access mode: {_sessionContext.AccessMode}");
            }

            return string.Join(Environment.NewLine, lines);
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
                Provider = provider ?? string.Empty,
                Model = model ?? string.Empty,
                DegradedFlags = (degradedFlags ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                FallbackReason = fallbackReason ?? string.Empty,
                FallbackMode = fallbackMode ?? string.Empty,
                FinalStatus = finalStatus ?? string.Empty,
                BuildSucceeded = buildSucceeded,
                BuildStarted = buildStarted,
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
                ApprovalRequiredActions = MapApprovalProposals(approvalRequiredActions),
                ExternalAttempts = approvalRequiredActions.Count,
                DeniedActions = tracerDeniedActions,
                BlockedActions = actionLifecycleEntries.Count(e => e.LifecycleState == ActionLifecycleState.Blocked),
                HostBoundaryPreserved = true,
                ActionLifecycle = MapActionLifecycle(actionLifecycleEntries)
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

            [JsonPropertyName("provider")]
            public string Provider { get; init; } = string.Empty;

            [JsonPropertyName("model")]
            public string Model { get; init; } = string.Empty;

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

            [JsonPropertyName("deniedActions")]
            public int DeniedActions { get; init; }

            [JsonPropertyName("blockedActions")]
            public int BlockedActions { get; init; }

            [JsonPropertyName("hostBoundaryPreserved")]
            public bool HostBoundaryPreserved { get; init; }

            [JsonPropertyName("actionLifecycle")]
            public ActionLifecyclePayload[] ActionLifecycle { get; init; } = Array.Empty<ActionLifecyclePayload>();
        }

        private static bool IsBroadEngineeringIntent(string task)
        {
            var value = (task ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Contains("implement") ||
                   value.Contains("build ") ||
                   value.Contains("create ") ||
                   value.Contains("converter") ||
                   value.Contains("поэтап") ||
                   value.Contains("разбор") ||
                   value.Contains("приступ");
        }

        private static ApprovalRequiredActionPayload[] MapApprovalProposals(IReadOnlyList<ActionApprovalProposal> proposals)
        {
            return proposals.Select(p => new ApprovalRequiredActionPayload
            {
                ActionType = p.ActionType,
                Command = p.Command ?? string.Empty,
                Path = p.Path ?? string.Empty,
                NormalizedTarget = p.NormalizedTarget ?? string.Empty,
                SandboxRoot = p.SandboxRoot,
                ProjectRoot = p.ProjectRoot,
                WorktreeRoot = p.WorktreeRoot,
                RiskLevel = p.RiskLevel,
                Reason = p.Reason,
                ApprovalStatus = p.ApprovalStatus.ToString()
            }).ToArray();
        }

        private sealed class ApprovalRequiredActionPayload
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
            [JsonPropertyName("reason")]
            public string Reason { get; init; } = string.Empty;
            [JsonPropertyName("approvalStatus")]
            public string ApprovalStatus { get; init; } = string.Empty;
        }

        private static ActionLifecyclePayload[] MapActionLifecycle(IReadOnlyList<ActionLifecycleEntry> entries)
        {
            return entries.Select(e => new ActionLifecyclePayload
            {
                Sequence = e.Sequence,
                ActionCorrelationId = e.ActionCorrelationId,
                ActionType = e.ActionType,
                Target = e.Target,
                Command = e.Command,
                NormalizedTarget = e.NormalizedTarget,
                LifecycleState = e.LifecycleState.ToString(),
                ReasonCode = e.ReasonCode,
                Reason = e.Reason,
                ApprovalStatus = e.ApprovalStatus,
                IsInsideSandbox = e.IsInsideSandbox
            }).ToArray();
        }

        private sealed class ActionLifecyclePayload
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

        private sealed class TimelinePayload
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

        private sealed class ChangedHint
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

        private sealed class ChangedKind
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

        private sealed class StructuredDiagnostic
        {
            public string RootCause { get; init; } = string.Empty;
            public string AttemptedFix { get; init; } = string.Empty;
            public string WhyDenied { get; init; } = string.Empty;
            public string NextSafeAction { get; init; } = string.Empty;
        }

    }
#pragma warning restore CS0162
}


