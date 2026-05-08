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

                var runState = new AgentRunState();
                var changedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var changedHints = new Dictionary<string, ChangedHint>(StringComparer.OrdinalIgnoreCase);
                var changedRanges = new Dictionary<string, ChangedRange>(StringComparer.OrdinalIgnoreCase);
                var changedKinds = new Dictionary<string, ChangedKind>(StringComparer.OrdinalIgnoreCase);
                var analysisOnlyTask = TaskPrecheckHeuristics.IsAnalysisOnlyTask(task);
                var runtimeClient = _llmClient as ILlmRuntimeClient;
                var runtimeMetadata = runtimeClient?.Metadata;
                var unrestrictedSandboxMode = AgentExecutionProfile.IsUnrestrictedInsideSandbox(_sessionContext);

                LogExecutionProfileIfNeeded(unrestrictedSandboxMode, tracer);
                LogIterationLoopStarted(tracer);

                for (int iteration = 0; iteration < MAX_ITERATIONS; iteration++)
                {
                    runState.ActualIterationsUsed = iteration + 1;
                    LogIterationStarted(tracer, runState.ActualIterationsUsed);

                    var preparedContextResult = await TryPrepareIterationContextAsync(task, analysisOnlyTask, gatedTargetFiles);
                    if (!preparedContextResult.Success)
                    {
                        return preparedContextResult.FailureResult!;
                    }

                    var preparedContext = preparedContextResult.PreparedContext!;
                    var resolvedFiles = preparedContext.ResolvedFiles;
                    var contextInfo = preparedContext.ContextInfo;
                    var contextString = preparedContext.ContextString;
                    runState.LastSuccessfulStep = "ContextBuilt";
                    runState.LastKnownAction = $"Built context with {resolvedFiles.Count} resolved files";

                    var (promptKind, prompt) = BuildIterationPrompt(task, analysisOnlyTask, iteration, runState.CurrentResponse, contextString, tracer);
                    runState.ModelCallStarted = true;
                    var modelRequest = await ExecuteModelRequestAsync(prompt, promptKind, iteration, runtimeClient, tracer);
                    var runtimeResult = modelRequest.RuntimeResult;
                    runState.CurrentResponse = modelRequest.Response;
                    runState.LastSuccessfulStep = "ModelRequestCompleted";
                    runState.LastKnownAction = "Model response received";

                    var modelDecision = HandleModelResponseDecision(
                        task,
                        analysisOnlyTask,
                        runtimeResult,
                        iteration,
                        runState.CurrentResponse,
                        contextInfo,
                        runStartedUtc,
                        runtimeMetadata,
                        changedFiles,
                        changedHints,
                        changedRanges,
                        changedKinds);
                    if (modelDecision.IsHandled)
                    {
                        return modelDecision.FinalResult!;
                    }

                    var toolHandling = await HandleIterationToolingAsync(
                        task,
                        analysisOnlyTask,
                        requestedNewFile,
                        runState.CurrentResponse,
                        resolvedFiles,
                        targetResolution,
                        runState.LastDeniedToolResult,
                        runState.LastBuildErrorSignature,
                        runState.LastBuildFailureCode,
                        changedFiles,
                        changedHints,
                        changedRanges,
                        changedKinds,
                        tracer);
                    runState.CurrentResponse = toolHandling.NextResponse;
                    runState.LastDeniedToolResult = toolHandling.LastDeniedToolResult;
                    runState.PatchStarted = runState.PatchStarted || toolHandling.PatchStarted;
                    if (toolHandling.BuildStarted)
                    {
                        runState.BuildStarted = true;
                    }

                    if (!string.IsNullOrWhiteSpace(toolHandling.LastSuccessfulStep))
                    {
                        runState.LastSuccessfulStep = toolHandling.LastSuccessfulStep!;
                    }

                    if (!string.IsNullOrWhiteSpace(toolHandling.LastKnownAction))
                    {
                        runState.LastKnownAction = toolHandling.LastKnownAction!;
                    }

                    runState.LastBuildErrorSignature = toolHandling.LastBuildErrorSignature;
                    runState.LastBuildFailureCode = toolHandling.LastBuildFailureCode;
                    runState.LastBuildExitCode = toolHandling.LastBuildExitCode;
                    runState.LastBuildTimedOut = toolHandling.LastBuildTimedOut;
                    runState.LastBuildErrorMessageTruncated = toolHandling.LastBuildErrorMessageTruncated;
                    runState.LastBuildErrorMessageLength = toolHandling.LastBuildErrorMessageLength;
                    if (toolHandling.FinalResult != null)
                    {
                        return toolHandling.FinalResult;
                    }

                    if (toolHandling.ShouldContinue)
                    {
                        continue;
                    }

                    LogIterationCompleted(tracer, runState.ActualIterationsUsed, runState.LastSuccessfulStep, runState.LastKnownAction);
                }

                return BuildTerminalFailureResult(tracer, runState);
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


