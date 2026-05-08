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
                var startupPreparation = await PrepareStartupAsync(task, requestedNewFile);
                if (!startupPreparation.Success)
                {
                    return startupPreparation.FailureResult!;
                }

                var targetResolution = startupPreparation.TargetResolution!;
                var gatedTargetFiles = startupPreparation.GatedTargetFiles;

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

                    var preparedContextResult = await TryPrepareIterationContextAsync(task, analysisOnlyTask, gatedTargetFiles);
                    if (!preparedContextResult.Success)
                    {
                        return preparedContextResult.FailureResult!;
                    }

                    var preparedContext = preparedContextResult.PreparedContext!;
                    var resolvedFiles = preparedContext.ResolvedFiles;
                    var contextInfo = preparedContext.ContextInfo;
                    var contextString = preparedContext.ContextString;
                    lastSuccessfulStep = "ContextBuilt";
                    lastKnownAction = $"Built context with {resolvedFiles.Count} resolved files";

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
                        var toolResultsProcessed = await ProcessToolResultsAsync(
                            task,
                            toolCalls,
                            resolvedFiles,
                            toolResults,
                            mutationCall,
                            lastDeniedToolResult,
                            changedFiles,
                            changedHints,
                            changedRanges,
                            changedKinds,
                            tracer);
                        if (toolResultsProcessed.FinalResult != null)
                        {
                            return toolResultsProcessed.FinalResult;
                        }

                        lastDeniedToolResult = toolResultsProcessed.LastDeniedToolResult;
                        var unknownToolError = toolResultsProcessed.UnknownToolError;

                        if (!string.IsNullOrWhiteSpace(unknownToolError))
                        {
                            currentResponse = $@"Tool call rejected: {unknownToolError}

Use only the registered tools exactly as listed in the prompt. The only valid tool names are 'file' and 'build'. If the task is analysis-only, respond directly without any tool call.";
                            continue;
                        }

                        if (mutationCall != null)
                        {
                            var buildVerification = await HandleMutationBuildVerificationAsync(
                                mutationCall,
                                changedFiles,
                                changedHints,
                                changedRanges,
                                changedKinds,
                                lastBuildErrorSignature,
                                lastBuildFailureCode);
                            if (buildVerification.BuildStarted)
                            {
                                buildStarted = true;
                                lastSuccessfulStep = buildVerification.LastSuccessfulStep;
                                lastKnownAction = buildVerification.LastKnownAction;
                            }

                            if (buildVerification.FinalResult != null)
                            {
                                return buildVerification.FinalResult;
                            }

                            lastBuildErrorSignature = buildVerification.LastBuildErrorSignature;
                            lastBuildFailureCode = buildVerification.LastBuildFailureCode;
                            lastBuildExitCode = buildVerification.LastBuildExitCode;
                            lastBuildTimedOut = buildVerification.LastBuildTimedOut;
                            lastBuildErrorMessageTruncated = buildVerification.LastBuildErrorMessageTruncated;
                            lastBuildErrorMessageLength = buildVerification.LastBuildErrorMessageLength;
                            if (buildVerification.NextResponse != null)
                            {
                                currentResponse = buildVerification.NextResponse;
                                continue;
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


