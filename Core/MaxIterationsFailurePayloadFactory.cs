namespace LocalCursorAgent.Core
{
    internal static class MaxIterationsFailurePayloadFactory
    {
        internal const string FailureCode = "MAX_ITERATIONS_REACHED";
        internal const string FailureStage = "AgentIterationLoop";
        internal const string FailureStep = "MaxIterationsReached";
        internal const string PipelineStoppedReason = "Iteration budget exhausted before completion";
        internal const string FailureMessage = "Max iterations reached. Task may not be fully complete.";

        internal static Agent.FailurePayload Create(
            bool buildStarted,
            string lastSuccessfulStep,
            string lastKnownAction,
            int actualIterationsUsed,
            int maxIterations,
            bool modelCallStarted,
            bool patchStarted,
            string buildFailureCode,
            int? buildExitCode,
            bool? buildTimedOut,
            bool? buildErrorMessageTruncated,
            int? buildErrorMessageLength)
        {
            return new Agent.FailurePayload
            {
                RootCauseCode = FailureCode,
                FailedStage = FailureStage,
                LastSuccessfulStep = lastSuccessfulStep,
                FailedStep = FailureStep,
                ReasonCode = FailureCode,
                Explanation = FailureMessage,
                PipelineStoppedReason = PipelineStoppedReason,
                DownstreamNotStarted = buildStarted ? string.Empty : "BuildVerification",
                LoopStage = FailureStage,
                MaxIterations = maxIterations,
                IterationsUsed = actualIterationsUsed,
                LastKnownAction = lastKnownAction,
                ModelCallStarted = modelCallStarted,
                PatchStarted = patchStarted,
                BuildStarted = buildStarted,
                BuildFailureCode = buildFailureCode,
                BuildExitCode = buildExitCode,
                BuildTimedOut = buildTimedOut,
                BuildErrorMessageTruncated = buildErrorMessageTruncated,
                BuildErrorMessageLength = buildErrorMessageLength,
                Timeline = TimelineBuilder.BuildMaxIterationsTimeline(actualIterationsUsed, maxIterations, lastSuccessfulStep, lastKnownAction)
            };
        }
    }
}
