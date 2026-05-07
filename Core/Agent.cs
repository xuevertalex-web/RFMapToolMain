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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalCursorAgent.Core
{
#pragma warning disable CS0162
    /// <summary>
    /// Main AI coding agent that orchestrates the tool-calling loop with semantic understanding.
    /// Integrates file state awareness for active context layer.
    /// </summary>
    public partial class Agent
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

            if (TryRejectTaskBeforeExecution(task, tracer, out var precheckResult))
            {
                return precheckResult;
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
                string? lastBuildFailureCode = null;
                int? lastBuildExitCode = null;
                bool? lastBuildTimedOut = null;
                bool? lastBuildErrorMessageTruncated = null;
                int? lastBuildErrorMessageLength = null;
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

                    var (promptKind, prompt) = BuildIterationPrompt(task, analysisOnlyTask, iteration, currentResponse, contextString, tracer);
                    modelCallStarted = true;
                    var modelRequest = await ExecuteModelRequestAsync(prompt, promptKind, iteration, runtimeClient, tracer);
                    var runtimeResult = modelRequest.RuntimeResult;
                    currentResponse = modelRequest.Response;
                    lastSuccessfulStep = "ModelRequestCompleted";
                    lastKnownAction = "Model response received";

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
                        return hardFailureResult;
                    }

                    if (TryHandleAnalysisDirectResponse(task, analysisOnlyTask, currentResponse, contextInfo, runStartedUtc, runtimeMetadata, out var analysisResult))
                    {
                        return analysisResult;
                    }

                    // Check for tool calls
                    if (_toolCaller.ContainsToolCalls(currentResponse))
                    {
                        var toolCalls = _toolCaller.ParseToolCalls(currentResponse);
                        lastSuccessfulStep = "ToolCallsParsed";
                        lastKnownAction = $"Parsed {toolCalls.Count} tool calls";
                        if (toolCalls.Count == 0)
                        {
                            var emptyToolDecision = HandleEmptyParsedToolCalls(task, analysisOnlyTask, currentResponse);
                            if (emptyToolDecision.IsHandled)
                            {
                                if (emptyToolDecision.ShouldContinue)
                                {
                                    currentResponse = emptyToolDecision.Payload;
                                    continue;
                                }

                                return emptyToolDecision.Payload;
                            }
                        }

                        var mutationIntentTask = MutationIntentDetector.IsMutationIntentTask(task) || requestedNewFile != null;

                        var mutationCall = toolCalls.FirstOrDefault(ToolCallMutationHeuristics.IsMutationLikeToolCall);
                        if (mutationCall != null &&
                            TryValidateMutationToolCalls(task, toolCalls, mutationCall, targetResolution, tracer, out var gateFailureResult))
                        {
                            return gateFailureResult;
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

                            await RecordWriteToolEffectsAsync(task, toolCalls, resolvedFiles, changedFiles, changedHints, changedRanges, changedKinds, tracer);
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
                                    var buildFailureCode = BuildFailureClassifier.Classify(buildResult);
                                    var failureMessage = BuildFailureMessageResolver.Resolve(buildResult, buildFailureCode);
                                    var errorMessage = failureMessage.Message;
                                    BuildFailureMemoryRecorder.Record(_memory, buildResult, buildFailureCode, failureMessage, errorMessage);
                                    var failureState = BuildFailureStateUpdater.From(buildResult, failureMessage, errorMessage);
                                    (lastBuildExitCode, lastBuildTimedOut, lastBuildErrorMessageTruncated, lastBuildErrorMessageLength) =
                                        BuildFailureStateAssignment.ToTuple(failureState);

                                    if (TryRepairCs8802(buildResult, changedFiles, out var repairPrompt))
                                    {
                                        currentResponse = repairPrompt ?? "Repaired CS8802-related issue. Re-run build and continue.";
                                        continue;
                                    }

                                    if (RepeatedBuildFailureDiagnosticFactory.TryCreate(
                                        lastBuildErrorSignature,
                                        lastBuildFailureCode,
                                        errorMessage,
                                        out var structuredBuildFailureCode,
                                        out var repeatedBuildFailure))
                                    {
                                        BuildFailureMemoryRecorder.RecordRepeatedFailureReasonCode(_memory, structuredBuildFailureCode);
                                        return FinalizeStructuredDiagnosticResult(
                                            structuredBuildFailureCode,
                                            RepeatedBuildFailureDiagnosticPayloadBuilder.Build(mutationCall.Input, repeatedBuildFailure),
                                            changedFiles,
                                            changedHints.Values,
                                            changedRanges.Values,
                                            changedKinds.Values);
                                    }

                                    lastBuildErrorSignature = errorMessage;
                                    lastBuildFailureCode = buildFailureCode;

                                    // Provide error context to LLM for next iteration
                                    currentResponse = BuildFailureRepairPromptBuilder.Build(buildFailureCode, errorMessage);
                                }
                            }
                        }

                        currentResponse = BuildPostToolContinuationResponse(
                            analysisOnlyTask,
                            mutationIntentTask,
                            mutationCall,
                            changedFiles.Count,
                            requestedNewFile,
                            currentResponse);
                    }
                    else
                    {
                        var noToolDecision = HandleNoToolCallResponse(task, currentResponse, requestedNewFile);
                        if (noToolDecision.IsHandled)
                        {
                            if (noToolDecision.ShouldContinue)
                            {
                                currentResponse = noToolDecision.Payload;
                                continue;
                            }

                            return noToolDecision.Payload;
                        }
                    }

                    tracer.LogActionEvent("IterationCompleted", "AgentIterationLoop", ExecutionTracer.ActionLogLevel.Info, "completed", metadata: new Dictionary<string, object?>
                    {
                        { "iteration", actualIterationsUsed },
                        { "max_iterations", MAX_ITERATIONS },
                        { "last_successful_step", lastSuccessfulStep },
                        { "last_known_action", lastKnownAction }
                    });
                }

                return FinalizeMaxIterationsFailure(
                    tracer,
                    actualIterationsUsed,
                    lastSuccessfulStep,
                    lastKnownAction,
                    modelCallStarted,
                    patchStarted,
                    buildStarted,
                    lastBuildFailureCode,
                    lastBuildExitCode,
                    lastBuildTimedOut,
                    lastBuildErrorMessageTruncated,
                    lastBuildErrorMessageLength);
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

    }
#pragma warning restore CS0162
}


