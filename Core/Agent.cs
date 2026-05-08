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

                LogExecutionProfileIfNeeded(unrestrictedSandboxMode, tracer);
                LogIterationLoopStarted(tracer);

                for (int iteration = 0; iteration < MAX_ITERATIONS; iteration++)
                {
                    actualIterationsUsed = iteration + 1;
                    LogIterationStarted(tracer, actualIterationsUsed);

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

                    var toolHandling = await HandleIterationToolingAsync(
                        task,
                        analysisOnlyTask,
                        requestedNewFile,
                        currentResponse,
                        resolvedFiles,
                        targetResolution,
                        lastDeniedToolResult,
                        lastBuildErrorSignature,
                        lastBuildFailureCode,
                        changedFiles,
                        changedHints,
                        changedRanges,
                        changedKinds,
                        tracer);
                    currentResponse = toolHandling.NextResponse;
                    lastDeniedToolResult = toolHandling.LastDeniedToolResult;
                    patchStarted = patchStarted || toolHandling.PatchStarted;
                    if (toolHandling.BuildStarted)
                    {
                        buildStarted = true;
                    }

                    if (!string.IsNullOrWhiteSpace(toolHandling.LastSuccessfulStep))
                    {
                        lastSuccessfulStep = toolHandling.LastSuccessfulStep!;
                    }

                    if (!string.IsNullOrWhiteSpace(toolHandling.LastKnownAction))
                    {
                        lastKnownAction = toolHandling.LastKnownAction!;
                    }

                    lastBuildErrorSignature = toolHandling.LastBuildErrorSignature;
                    lastBuildFailureCode = toolHandling.LastBuildFailureCode;
                    lastBuildExitCode = toolHandling.LastBuildExitCode;
                    lastBuildTimedOut = toolHandling.LastBuildTimedOut;
                    lastBuildErrorMessageTruncated = toolHandling.LastBuildErrorMessageTruncated;
                    lastBuildErrorMessageLength = toolHandling.LastBuildErrorMessageLength;
                    if (toolHandling.FinalResult != null)
                    {
                        return toolHandling.FinalResult;
                    }

                    if (toolHandling.ShouldContinue)
                    {
                        continue;
                    }

                    LogIterationCompleted(tracer, actualIterationsUsed, lastSuccessfulStep, lastKnownAction);
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


