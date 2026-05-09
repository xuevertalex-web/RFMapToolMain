using LocalCursorAgent.LLM;
using LocalCursorAgent.Memory;
using LocalCursorAgent.Execution;
using LocalCursorAgent.Tools;
using LocalCursorAgent.Indexing;
using LocalCursorAgent.Context;
using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Security;
using LocalCursorAgent.LLM.Runtime;

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
        private int _lastLlmRetryCount;
        private string _lastLlmErrorType = string.Empty;

        private const int MAX_ITERATIONS = 3;
        private const int CONTEXT_WINDOW = 15;
        private const int CONTEXT_EXPANSION_BUFFER = 5;

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
            var context = new RunContext(task);

            try
            {
                var precheck = PrecheckStage(context);
                if (precheck.ShouldReturn)
                    return precheck.FinalResult!;

                var indexing = await IndexingStage(context);
                if (indexing.ShouldReturn)
                    return indexing.FinalResult!;

                TargetResolutionStage(context);
                SandboxSetupStage(context);
                var contextBuild = ContextBuildStage(context);
                if (contextBuild.ShouldReturn)
                    return contextBuild.FinalResult!;

                for (int iteration = 0; iteration < MAX_ITERATIONS; iteration++)
                {
                    context.RunState.ActualIterationsUsed = iteration + 1;
                    LogIterationStarted(context.Tracer!, context.RunState.ActualIterationsUsed);

                    var promptBuild = PromptBuildStage(context, iteration);
                    if (promptBuild.ShouldReturn)
                        return promptBuild.FinalResult!;

                    var llmCall = await LlmCallStage(context, iteration);
                    if (llmCall.ShouldReturn)
                        return llmCall.FinalResult!;

                    var toolProcessing = await ToolProcessingStage(context);
                    if (toolProcessing.ShouldReturn)
                        return toolProcessing.FinalResult!;

                    var mutation = MutationStage(context);
                    if (mutation.ShouldContinue)
                        continue;

                    VerificationStage(context);
                    RepairLoopStage(context);
                    LogIterationCompleted(context.Tracer!, context.RunState.ActualIterationsUsed, context.RunState.LastSuccessfulStep, context.RunState.LastKnownAction);
                }

                return FinalizationStage(context);
            }
            catch (Exception ex)
            {
                var error = $"Agent error: {ex.Message}";
                _memory.Add("error", error, "UnhandledException");
                context.Tracer!.LogActionEvent("TaskLifecycle", "Agent", ExecutionTracer.ActionLogLevel.Error, "failed", "UNHANDLED_EXCEPTION", new Dictionary<string, object?>
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


