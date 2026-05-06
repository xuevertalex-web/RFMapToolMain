namespace LocalCursorAgent.Core
{
    internal static class MaxIterationsFailurePayloadFactory
    {
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
                RootCauseCode = "MAX_ITERATIONS_REACHED",
                FailedStage = "AgentIterationLoop",
                LastSuccessfulStep = lastSuccessfulStep,
                FailedStep = "MaxIterationsReached",
                ReasonCode = "MAX_ITERATIONS_REACHED",
                Explanation = "Max iterations reached. Task may not be fully complete.",
                PipelineStoppedReason = "Iteration budget exhausted before completion",
                DownstreamNotStarted = buildStarted ? string.Empty : "BuildVerification",
                LoopStage = "AgentIterationLoop",
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
