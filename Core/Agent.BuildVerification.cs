namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class MutationBuildVerificationResult
        {
            public required bool BuildStarted { get; init; }
            public required string LastSuccessfulStep { get; init; }
            public required string LastKnownAction { get; init; }
            public string? LastBuildErrorSignature { get; init; }
            public string? LastBuildFailureCode { get; init; }
            public int? LastBuildExitCode { get; init; }
            public bool? LastBuildTimedOut { get; init; }
            public bool? LastBuildErrorMessageTruncated { get; init; }
            public int? LastBuildErrorMessageLength { get; init; }
            public string? NextResponse { get; init; }
            public string? FinalResult { get; init; }
        }

        private async Task<MutationBuildVerificationResult> HandleMutationBuildVerificationAsync(
            ToolCaller.ToolCall mutationCall,
            HashSet<string> changedFiles,
            Dictionary<string, ChangedHint> changedHints,
            Dictionary<string, ChangedRange> changedRanges,
            Dictionary<string, ChangedKind> changedKinds,
            string? lastBuildErrorSignature,
            string? lastBuildFailureCode)
        {
            var buildPath = _sessionContext?.ActiveWorkspaceRoot;
            if (string.IsNullOrWhiteSpace(buildPath) || !Directory.Exists(buildPath))
            {
                return new MutationBuildVerificationResult
                {
                    BuildStarted = false,
                    LastSuccessfulStep = "ToolCallsExecuted",
                    LastKnownAction = "Executed tool calls",
                    LastBuildErrorSignature = lastBuildErrorSignature,
                    LastBuildFailureCode = lastBuildFailureCode
                };
            }

            var buildResult = await _buildVerifier.VerifyBuild(buildPath);
            _contextBuilder.Tracer.LogBuildVerificationResult(buildResult);

            if (buildResult.Success)
            {
                _memory.Add("build_status", "success");

                if (changedFiles.Count == 0)
                {
                    _memory.Add("task_status", "no_op_after_build");
                    _sandboxManager.CleanupSandbox();
                    return new MutationBuildVerificationResult
                    {
                        BuildStarted = true,
                        LastSuccessfulStep = "BuildVerificationCompleted",
                        LastKnownAction = "Build verification succeeded",
                        LastBuildErrorSignature = lastBuildErrorSignature,
                        LastBuildFailureCode = lastBuildFailureCode,
                        FinalResult = FinalizeRunResult(
                            true,
                            "Build succeeded but no file changes were made.",
                            "Agent completed with no-op after successful build",
                            "NO_OP_SUCCESS",
                            Array.Empty<string>(),
                            Array.Empty<ChangedHint>(),
                            Array.Empty<ChangedRange>(),
                            Array.Empty<ChangedKind>(),
                            true)
                    };
                }

                if (VERBOSE_OUTPUT)
                {
                    Console.WriteLine("Changes are already written in the active workspace");
                }

                _memory.Add("task_status", "completed");
                _sandboxManager.CleanupSandbox();
                return new MutationBuildVerificationResult
                {
                    BuildStarted = true,
                    LastSuccessfulStep = "BuildVerificationCompleted",
                    LastKnownAction = "Build verification succeeded",
                    LastBuildErrorSignature = lastBuildErrorSignature,
                    LastBuildFailureCode = lastBuildFailureCode,
                    FinalResult = FinalizeRunResult(
                        true,
                        "Task completed successfully",
                        "Agent completed task in the active workspace",
                        "SUCCESS",
                        changedFiles,
                        changedHints.Values,
                        changedRanges.Values,
                        changedKinds.Values,
                        true)
                };
            }

            var buildFailureCode = BuildFailureClassifier.Classify(buildResult);
            var failureMessage = BuildFailureMessageResolver.Resolve(buildResult, buildFailureCode);
            var errorMessage = failureMessage.Message;
            BuildFailureMemoryRecorder.Record(_memory, buildResult, buildFailureCode, failureMessage, errorMessage);
            var failureState = BuildFailureStateUpdater.From(buildResult, failureMessage, errorMessage);
            var (exitCode, timedOut, errorMessageTruncated, errorMessageLength) = BuildFailureStateAssignment.ToTuple(failureState);

            if (TryRepairCs8802(buildResult, changedFiles, out var repairPrompt))
            {
                return new MutationBuildVerificationResult
                {
                    BuildStarted = true,
                    LastSuccessfulStep = "BuildVerificationCompleted",
                    LastKnownAction = "Build verification failed",
                    LastBuildErrorSignature = lastBuildErrorSignature,
                    LastBuildFailureCode = lastBuildFailureCode,
                    LastBuildExitCode = exitCode,
                    LastBuildTimedOut = timedOut,
                    LastBuildErrorMessageTruncated = errorMessageTruncated,
                    LastBuildErrorMessageLength = errorMessageLength,
                    NextResponse = repairPrompt ?? "Repaired CS8802-related issue. Re-run build and continue."
                };
            }

            if (RepeatedBuildFailureDiagnosticFactory.TryCreate(
                lastBuildErrorSignature,
                lastBuildFailureCode,
                errorMessage,
                out var structuredBuildFailureCode,
                out var repeatedBuildFailure))
            {
                BuildFailureMemoryRecorder.RecordRepeatedFailureReasonCode(_memory, structuredBuildFailureCode);
                return new MutationBuildVerificationResult
                {
                    BuildStarted = true,
                    LastSuccessfulStep = "BuildVerificationCompleted",
                    LastKnownAction = "Build verification failed",
                    LastBuildErrorSignature = errorMessage,
                    LastBuildFailureCode = buildFailureCode,
                    LastBuildExitCode = exitCode,
                    LastBuildTimedOut = timedOut,
                    LastBuildErrorMessageTruncated = errorMessageTruncated,
                    LastBuildErrorMessageLength = errorMessageLength,
                    FinalResult = FinalizeStructuredDiagnosticResult(
                        structuredBuildFailureCode,
                        RepeatedBuildFailureDiagnosticPayloadBuilder.Build(mutationCall.Input, repeatedBuildFailure),
                        changedFiles,
                        changedHints.Values,
                        changedRanges.Values,
                        changedKinds.Values)
                };
            }

            return new MutationBuildVerificationResult
            {
                BuildStarted = true,
                LastSuccessfulStep = "BuildVerificationCompleted",
                LastKnownAction = "Build verification failed",
                LastBuildErrorSignature = errorMessage,
                LastBuildFailureCode = buildFailureCode,
                LastBuildExitCode = exitCode,
                LastBuildTimedOut = timedOut,
                LastBuildErrorMessageTruncated = errorMessageTruncated,
                LastBuildErrorMessageLength = errorMessageLength,
                NextResponse = BuildFailureRepairPromptBuilder.Build(buildFailureCode, errorMessage)
            };
        }
    }
}
