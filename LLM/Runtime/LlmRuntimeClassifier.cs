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

            if (LooksLikeUsableErrorPrefixedResponse(trimmed, profile))
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
            if (ContainsAny(text, "returned status notfound", "returned status not found", "returned status 404", "status notfound", "status 404", "model not found"))
                return (LlmRuntimeStatus.ProviderUnavailable, LlmTimeoutKind.None);
            if (ContainsAny(text, "unable to reach", "service unavailable", "unavailable due to", "connection refused", "no such host"))
                return (LlmRuntimeStatus.ProviderUnavailable, LlmTimeoutKind.None);

            return (LlmRuntimeStatus.LlmRequestFailed, LlmTimeoutKind.None);
        }

        private static bool LooksLikeHardError(string text)
        {
            return text.StartsWith("Error:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeUsableErrorPrefixedResponse(string text, LlmRuntimeProfile profile)
        {
            if (!text.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                return false;

            if (LooksLikeProviderFailureSignature(text))
                return false;

            var payload = text["Error:".Length..].Trim();
            if (string.IsNullOrWhiteSpace(payload))
                return false;

            if (ContainsAny(
                    text,
                    "here is",
                    "analysis:",
                    "РєСЂР°С‚РєРёР№ РѕР±Р·РѕСЂ",
                    "РїСЂРѕРµРєС‚",
                    "based on indexed"))
            {
                return true;
            }

            if (!IsHighTolerancePlainTextAnalysisProfile(profile))
                return false;

            if (LooksLikeProviderFailureSignature(payload))
                return false;

            return HasTersePlainTextAnalysisSignal(payload, profile);
        }

        private static bool IsHighTolerancePlainTextAnalysisProfile(LlmRuntimeProfile profile)
        {
            var highTolerance = profile.UsableTextTolerance.Equals("high", StringComparison.OrdinalIgnoreCase) ||
                                profile.UsableTextTolerance.Equals("very_high", StringComparison.OrdinalIgnoreCase);
            if (!highTolerance)
                return false;

            return profile.ExpectedAnalysisResponseMode.StartsWith("plain_text", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasTersePlainTextAnalysisSignal(string payload, LlmRuntimeProfile profile)
        {
            var tokenCount = CountWordLikeTokens(payload);
            if (tokenCount < 5)
                return false;

            if (IsTersePlainTextInstructProfile(profile))
            {
                if (ContainsAny(
                        payload,
                        "error",
                        "failed",
                        "failure",
                        "exception",
                        "timed out",
                        "timeout",
                        "stalled",
                        "stall",
                        "no progress",
                        "http",
                        "invalid",
                        "forbidden",
                        "unauthorized"))
                {
                    return false;
                }

                return payload.Contains('.', StringComparison.Ordinal) ||
                       payload.Contains(':', StringComparison.Ordinal) ||
                       payload.Contains(';', StringComparison.Ordinal);
            }

            if (!ContainsAny(
                    payload,
                    "analysis",
                    "summary",
                    "overview",
                    "project",
                    "code",
                    "entry",
                    "build",
                    "patch",
                    "module",
                    "class",
                    "file",
                    "\u0430\u043d\u0430\u043b\u0438\u0437",
                    "\u043e\u0431\u0437\u043e\u0440",
                    "\u043f\u0440\u043e\u0435\u043a\u0442",
                    "\u043a\u043e\u0434",
                    "\u0444\u0430\u0439\u043b",
                    "\u0441\u0431\u043e\u0440\u043a"))
            {
                return false;
            }

            return payload.Contains('.', StringComparison.Ordinal) ||
                   payload.Contains(':', StringComparison.Ordinal) ||
                   payload.Contains(';', StringComparison.Ordinal);
        }

        private static bool IsTersePlainTextInstructProfile(LlmRuntimeProfile profile)
        {
            return profile.ExpectedAnalysisResponseMode.Equals("plain_text_terse_ok", StringComparison.OrdinalIgnoreCase);
        }

        private static int CountWordLikeTokens(string payload)
        {
            return payload
                .Split(new[] { ' ', '\t', '\r', '\n', ',', ';', ':', '.', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(token => token.Any(char.IsLetterOrDigit));
        }

        private static bool LooksLikeProviderFailureSignature(string text)
        {
            return ContainsAny(
                text,
                "request failed",
                "request timed out",
                "timed out",
                "timeout",
                "unable to reach",
                "returned status",
                "unexpected response format",
                "no response from",
                "empty prompt",
                "service unavailable",
                "unavailable due to",
                "connection refused",
                "no such host",
                "failed to connect",
                "request canceled",
                "request cancelled");
        }

        private static bool ContainsAny(string text, params string[] tokens)
        {
            return tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
        }
    }
}
