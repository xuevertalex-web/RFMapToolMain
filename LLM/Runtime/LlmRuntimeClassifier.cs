namespace LocalCursorAgent.LLM.Runtime
{
    public static class LlmRuntimeClassifier
    {
        public static LlmRuntimeResult Classify(
            string rawResponse,
            LlmProviderMetadata metadata,
            LlmRuntimeProfile profile,
            LlmRuntimePolicy policy)
        {
            var response = rawResponse ?? string.Empty;
            var trimmed = response.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return new LlmRuntimeResult(
                    Completion: string.Empty,
                    Status: LlmRuntimeStatus.ModelOutputEmpty,
                    Metadata: metadata,
                    Profile: profile,
                    Policy: policy,
                    IsUsable: false,
                    IsFailure: true,
                    TimeoutKind: LlmTimeoutKind.None,
                    FailureMessage: "MODEL_OUTPUT_EMPTY");
            }

            if (!LooksLikeHardError(trimmed))
            {
                return new LlmRuntimeResult(
                    Completion: trimmed,
                    Status: LlmRuntimeStatus.Success,
                    Metadata: metadata,
                    Profile: profile,
                    Policy: policy,
                    IsUsable: true,
                    IsFailure: false,
                    TimeoutKind: LlmTimeoutKind.None,
                    FailureMessage: null);
            }

            if (LooksLikeUsableErrorPrefixedResponse(trimmed))
            {
                return new LlmRuntimeResult(
                    Completion: trimmed,
                    Status: policy.TreatUsablePartialOutputAsSuccessForAnalysis
                        ? LlmRuntimeStatus.Success
                        : LlmRuntimeStatus.PartialOutput,
                    Metadata: metadata,
                    Profile: profile,
                    Policy: policy,
                    IsUsable: true,
                    IsFailure: false,
                    TimeoutKind: LlmTimeoutKind.None,
                    FailureMessage: null);
            }

            var (status, timeoutKind) = ClassifyFailure(trimmed);
            return new LlmRuntimeResult(
                Completion: trimmed,
                Status: status,
                Metadata: metadata,
                Profile: profile,
                Policy: policy,
                IsUsable: false,
                IsFailure: true,
                TimeoutKind: timeoutKind,
                FailureMessage: trimmed);
        }

        private static (LlmRuntimeStatus Status, LlmTimeoutKind TimeoutKind) ClassifyFailure(string text)
        {
            if (ContainsAny(text, "stall timeout", "no progress"))
                return (LlmRuntimeStatus.ModelTimeout, LlmTimeoutKind.Stall);
            if (ContainsAny(text, "stalled", "progress stalled", "response stalled"))
                return (LlmRuntimeStatus.ModelTimeout, LlmTimeoutKind.Stall);
            if (ContainsAny(text, "connect timeout", "connection timeout", "failed to connect"))
                return (LlmRuntimeStatus.ModelTimeout, LlmTimeoutKind.ConnectStart);
            if (ContainsAny(text, "overall safety budget", "overall timeout"))
                return (LlmRuntimeStatus.ModelTimeout, LlmTimeoutKind.OverallSafetyBudget);
            if (ContainsAny(text, "timed out", "timeout"))
                return (LlmRuntimeStatus.ModelTimeout, LlmTimeoutKind.FirstResponse);
            if (ContainsAny(text, "unable to reach", "service unavailable", "connection refused", "no such host"))
                return (LlmRuntimeStatus.ProviderUnavailable, LlmTimeoutKind.None);

            return (LlmRuntimeStatus.LlmRequestFailed, LlmTimeoutKind.None);
        }

        private static bool LooksLikeHardError(string text)
        {
            return text.StartsWith("Error:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeUsableErrorPrefixedResponse(string text)
        {
            if (!text.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                return false;

            return ContainsAny(
                text,
                "here is",
                "analysis:",
                "краткий обзор",
                "проект",
                "based on indexed");
        }

        private static bool ContainsAny(string text, params string[] tokens)
        {
            return tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
        }
    }
}
