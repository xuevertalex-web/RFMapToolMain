using LocalCursorAgent.Context;
using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Execution;
using LocalCursorAgent.LLM.Runtime;
using LocalCursorAgent.Security;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private RunStageResult PrecheckStage(RunContext context)
        {
            var bootstrap = PrepareRunTaskBootstrap(context.Task);
            if (bootstrap.IsRejected)
                return RunStageResult.Return(bootstrap.RejectedResult!);

            context.RunStartedUtc = bootstrap.RunStartedUtc;
            context.Tracer = bootstrap.Tracer;
            context.RequestedNewFile = bootstrap.RequestedNewFile;
            context.AnalysisOnlyTask = TaskPrecheckHeuristics.IsAnalysisOnlyTask(context.Task);
            context.RuntimeClient = _llmClient as ILlmRuntimeClient;
            context.RuntimeMetadata = context.RuntimeClient?.Metadata;
            return RunStageResult.Continue();
        }

        private async Task<RunStageResult> IndexingStage(RunContext context)
        {
            var startupPreparation = await PrepareStartupAsync(context.Task, context.RequestedNewFile);
            if (!startupPreparation.Success)
                return RunStageResult.Return(startupPreparation.FailureResult!);

            context.TargetResolution = startupPreparation.TargetResolution!;
            context.GatedTargetFiles = startupPreparation.GatedTargetFiles;
            return RunStageResult.Continue();
        }

        private void TargetResolutionStage(RunContext context) { }

        private void SandboxSetupStage(RunContext context)
        {
            var unrestrictedSandboxMode = AgentExecutionProfile.IsUnrestrictedInsideSandbox(_sessionContext);
            LogExecutionProfileIfNeeded(unrestrictedSandboxMode, context.Tracer!);
            LogIterationLoopStarted(context.Tracer!);
        }

        private RunStageResult ContextBuildStage(RunContext context) => RunStageResult.Continue();

        private RunStageResult PromptBuildStage(RunContext context, int iteration) => RunStageResult.Continue();

        private async Task<RunStageResult> LlmCallStage(RunContext context, int iteration)
        {
            var preparedContextResult = await TryPrepareIterationContextAsync(context.Task, context.AnalysisOnlyTask, context.GatedTargetFiles!, context.TargetResolution!);
            if (!preparedContextResult.Success)
                return RunStageResult.Return(preparedContextResult.FailureResult!);

            var preparedContext = preparedContextResult.PreparedContext!;
            context.ResolvedFiles = preparedContext.ResolvedFiles;
            context.ContextInfo = preparedContext.ContextInfo;
            context.RunState.LastSuccessfulStep = "ContextBuilt";
            context.RunState.LastKnownAction = $"Built context with {context.ResolvedFiles.Count} resolved files";

            var (promptKind, prompt) = BuildIterationPrompt(context.Task, context.AnalysisOnlyTask, iteration, context.RunState.CurrentResponse, preparedContext.ContextString, context.Tracer!);
            context.RunState.ModelCallStarted = true;
            var modelRequest = await ExecuteModelRequestAsync(prompt, promptKind, iteration, context.RuntimeClient, context.Tracer!);
            context.RunState.CurrentResponse = modelRequest.Response;
            context.RunState.LlmRetryCount = _lastLlmRetryCount;
            context.RunState.LlmErrorType = _lastLlmErrorType;
            context.RunState.LastSuccessfulStep = "ModelRequestCompleted";
            context.RunState.LastKnownAction = "Model response received";

            var modelDecision = HandleModelResponseDecision(
                context.Task,
                context.AnalysisOnlyTask,
                modelRequest.RuntimeResult,
                iteration,
                context.RunState.CurrentResponse,
                context.ContextInfo!,
                context.RunStartedUtc ?? DateTime.UtcNow,
                context.RuntimeMetadata,
                context.ChangedFiles,
                context.ChangedHints,
                context.ChangedRanges,
                context.ChangedKinds);
            return modelDecision.IsHandled ? RunStageResult.Return(modelDecision.FinalResult!) : RunStageResult.Continue();
        }

        private async Task<RunStageResult> ToolProcessingStage(RunContext context)
        {
            var toolHandling = await HandleIterationToolingAsync(
                context.Task,
                context.AnalysisOnlyTask,
                context.RequestedNewFile,
                context.RunState.CurrentResponse,
                context.ResolvedFiles!,
                context.TargetResolution!,
                context.RunState.LastDeniedToolResult,
                context.RunState.LastBuildErrorSignature,
                context.RunState.LastBuildFailureCode,
                context.ChangedFiles,
                context.ChangedHints,
                context.ChangedRanges,
                context.ChangedKinds,
                context.Tracer!);
            context.ToolingApply = ApplyToolHandlingToRunState(context.RunState, toolHandling);
            return context.ToolingApply.ShouldReturn ? RunStageResult.Return(context.ToolingApply.FinalResult!) : RunStageResult.Continue();
        }

        private MutationStageResult MutationStage(RunContext context)
        {
            return new MutationStageResult(context.ToolingApply!.ShouldContinue);
        }

        private void VerificationStage(RunContext context) { }
        private void RepairLoopStage(RunContext context) { }

        private string FinalizationStage(RunContext context)
        {
            return BuildTerminalFailureResult(context.Tracer!, context.RunState);
        }
    }
}
