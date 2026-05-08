namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private async Task<MutationContinuationFlowResult> HandleMutationContinuationFlowAsync(
            ToolCaller.ToolCall? mutationCall,
            HashSet<string> changedFiles,
            Dictionary<string, ChangedHint> changedHints,
            Dictionary<string, ChangedRange> changedRanges,
            Dictionary<string, ChangedKind> changedKinds,
            string currentResponse,
            bool patchStarted,
            string? lastDeniedToolResult,
            string? lastBuildErrorSignature,
            string? lastBuildFailureCode,
            bool buildStarted,
            int? lastBuildExitCode,
            bool? lastBuildTimedOut,
            bool? lastBuildErrorMessageTruncated,
            int? lastBuildErrorMessageLength,
            string lastSuccessfulStep,
            string lastKnownAction)
        {
            if (mutationCall == null)
            {
                return new MutationContinuationFlowResult(
                    ShouldReturn: false,
                    Result: null,
                    BuildStarted: buildStarted,
                    LastBuildErrorSignature: lastBuildErrorSignature,
                    LastBuildFailureCode: lastBuildFailureCode,
                    LastBuildExitCode: lastBuildExitCode,
                    LastBuildTimedOut: lastBuildTimedOut,
                    LastBuildErrorMessageTruncated: lastBuildErrorMessageTruncated,
                    LastBuildErrorMessageLength: lastBuildErrorMessageLength,
                    LastSuccessfulStep: lastSuccessfulStep,
                    LastKnownAction: lastKnownAction);
            }

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
                return new MutationContinuationFlowResult(
                    ShouldReturn: true,
                    Result: new IterationToolingResult
                    {
                        NextResponse = currentResponse,
                        ShouldContinue = false,
                        FinalResult = buildVerification.FinalResult,
                        PatchStarted = patchStarted,
                        BuildStarted = buildStarted,
                        LastDeniedToolResult = lastDeniedToolResult,
                        LastBuildErrorSignature = buildVerification.LastBuildErrorSignature,
                        LastBuildFailureCode = buildVerification.LastBuildFailureCode,
                        LastBuildExitCode = buildVerification.LastBuildExitCode,
                        LastBuildTimedOut = buildVerification.LastBuildTimedOut,
                        LastBuildErrorMessageTruncated = buildVerification.LastBuildErrorMessageTruncated,
                        LastBuildErrorMessageLength = buildVerification.LastBuildErrorMessageLength,
                        LastSuccessfulStep = lastSuccessfulStep,
                        LastKnownAction = lastKnownAction
                    },
                    BuildStarted: buildStarted,
                    LastBuildErrorSignature: buildVerification.LastBuildErrorSignature,
                    LastBuildFailureCode: buildVerification.LastBuildFailureCode,
                    LastBuildExitCode: buildVerification.LastBuildExitCode,
                    LastBuildTimedOut: buildVerification.LastBuildTimedOut,
                    LastBuildErrorMessageTruncated: buildVerification.LastBuildErrorMessageTruncated,
                    LastBuildErrorMessageLength: buildVerification.LastBuildErrorMessageLength,
                    LastSuccessfulStep: lastSuccessfulStep,
                    LastKnownAction: lastKnownAction);
            }

            lastBuildErrorSignature = buildVerification.LastBuildErrorSignature;
            lastBuildFailureCode = buildVerification.LastBuildFailureCode;
            lastBuildExitCode = buildVerification.LastBuildExitCode;
            lastBuildTimedOut = buildVerification.LastBuildTimedOut;
            lastBuildErrorMessageTruncated = buildVerification.LastBuildErrorMessageTruncated;
            lastBuildErrorMessageLength = buildVerification.LastBuildErrorMessageLength;
            if (buildVerification.NextResponse != null)
            {
                return new MutationContinuationFlowResult(
                    ShouldReturn: true,
                    Result: BuildMutationContinuationResult(
                        buildVerification.NextResponse,
                        patchStarted,
                        buildStarted,
                        lastDeniedToolResult,
                        lastBuildErrorSignature,
                        lastBuildFailureCode,
                        lastBuildExitCode,
                        lastBuildTimedOut,
                        lastBuildErrorMessageTruncated,
                        lastBuildErrorMessageLength,
                        lastSuccessfulStep,
                        lastKnownAction),
                    BuildStarted: buildStarted,
                    LastBuildErrorSignature: lastBuildErrorSignature,
                    LastBuildFailureCode: lastBuildFailureCode,
                    LastBuildExitCode: lastBuildExitCode,
                    LastBuildTimedOut: lastBuildTimedOut,
                    LastBuildErrorMessageTruncated: lastBuildErrorMessageTruncated,
                    LastBuildErrorMessageLength: lastBuildErrorMessageLength,
                    LastSuccessfulStep: lastSuccessfulStep,
                    LastKnownAction: lastKnownAction);
            }

            return new MutationContinuationFlowResult(
                ShouldReturn: false,
                Result: null,
                BuildStarted: buildStarted,
                LastBuildErrorSignature: lastBuildErrorSignature,
                LastBuildFailureCode: lastBuildFailureCode,
                LastBuildExitCode: lastBuildExitCode,
                LastBuildTimedOut: lastBuildTimedOut,
                LastBuildErrorMessageTruncated: lastBuildErrorMessageTruncated,
                LastBuildErrorMessageLength: lastBuildErrorMessageLength,
                LastSuccessfulStep: lastSuccessfulStep,
                LastKnownAction: lastKnownAction);
        }

        private sealed record MutationContinuationFlowResult(
            bool ShouldReturn,
            IterationToolingResult? Result,
            bool BuildStarted,
            string? LastBuildErrorSignature,
            string? LastBuildFailureCode,
            int? LastBuildExitCode,
            bool? LastBuildTimedOut,
            bool? LastBuildErrorMessageTruncated,
            int? LastBuildErrorMessageLength,
            string LastSuccessfulStep,
            string LastKnownAction);
    }
}
