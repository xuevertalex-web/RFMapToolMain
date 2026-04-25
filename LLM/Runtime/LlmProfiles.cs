namespace LocalCursorAgent.LLM.Runtime
{
    public static class LlmProfiles
    {
        private static readonly Dictionary<string, LlmRuntimeProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ollama/default"] = new(
                ProfileId: "ollama/default",
                Provider: "ollama",
                ContextClass: "local-medium",
                ExpectedLatencyClass: "slow-local",
                StreamingSupported: false,
                StructuredOutputReliability: "medium",
                CodeStrength: "medium",
                ParsingStrictness: "balanced",
                UsableTextTolerance: "high",
                ExpectedAnalysisResponseMode: "plain_text",
                TimeoutProfile: "local_ollama_relaxed",
                StallProfile: "local_ollama_progress_aware",
                RetryPreference: "provider-fallback",
                PreferFallbackOnHardFailure: true),
            ["ollama/qwen2.5-coder"] = new(
                ProfileId: "ollama/qwen2.5-coder",
                Provider: "ollama",
                ContextClass: "local-medium",
                ExpectedLatencyClass: "slow-local",
                StreamingSupported: false,
                StructuredOutputReliability: "medium",
                CodeStrength: "high",
                ParsingStrictness: "balanced",
                UsableTextTolerance: "high",
                ExpectedAnalysisResponseMode: "plain_text",
                TimeoutProfile: "local_ollama_relaxed",
                StallProfile: "local_ollama_progress_aware",
                RetryPreference: "provider-fallback",
                PreferFallbackOnHardFailure: true),
            ["ollama/qwen2.5-coder-instruct-q4_k_m"] = new(
                ProfileId: "ollama/qwen2.5-coder-instruct-q4_k_m",
                Provider: "ollama",
                ContextClass: "local-medium",
                ExpectedLatencyClass: "slow-local",
                StreamingSupported: false,
                StructuredOutputReliability: "medium",
                CodeStrength: "high",
                ParsingStrictness: "lenient",
                UsableTextTolerance: "very_high",
                ExpectedAnalysisResponseMode: "plain_text_terse_ok",
                TimeoutProfile: "local_ollama_relaxed",
                StallProfile: "local_ollama_progress_aware",
                RetryPreference: "provider-fallback",
                PreferFallbackOnHardFailure: true),
            ["openai/default"] = new(
                ProfileId: "openai/default",
                Provider: "openai",
                ContextClass: "remote-large",
                ExpectedLatencyClass: "medium",
                StreamingSupported: false,
                StructuredOutputReliability: "high",
                CodeStrength: "high",
                ParsingStrictness: "strict",
                UsableTextTolerance: "medium",
                ExpectedAnalysisResponseMode: "structured_or_text",
                TimeoutProfile: "remote_standard",
                StallProfile: "remote_standard",
                RetryPreference: "provider-fallback",
                PreferFallbackOnHardFailure: true),
            ["gemini/default"] = new(
                ProfileId: "gemini/default",
                Provider: "gemini",
                ContextClass: "remote-large",
                ExpectedLatencyClass: "medium-high",
                StreamingSupported: false,
                StructuredOutputReliability: "medium-high",
                CodeStrength: "high",
                ParsingStrictness: "strict",
                UsableTextTolerance: "medium",
                ExpectedAnalysisResponseMode: "structured_or_text",
                TimeoutProfile: "remote_gemini_standard",
                StallProfile: "remote_gemini_standard",
                RetryPreference: "provider-fallback",
                PreferFallbackOnHardFailure: true)
        };

        private static readonly LlmRuntimePolicy OllamaRelaxedPolicy = new(
            ConnectStartTimeout: TimeSpan.FromSeconds(20),
            FirstResponseTimeout: TimeSpan.FromSeconds(180),
            StallTimeout: TimeSpan.FromSeconds(90),
            OverallSafetyBudget: TimeSpan.FromMinutes(6),
            TreatUsablePartialOutputAsSuccessForAnalysis: true);
        private static readonly LlmRuntimePolicy GeminiStandardPolicy = new(
            ConnectStartTimeout: TimeSpan.FromSeconds(15),
            FirstResponseTimeout: TimeSpan.FromSeconds(120),
            StallTimeout: TimeSpan.FromSeconds(70),
            OverallSafetyBudget: TimeSpan.FromMinutes(4),
            TreatUsablePartialOutputAsSuccessForAnalysis: true);

        public static LlmRuntimeProfile Resolve(string provider, string? model = null)
        {
            if (provider.Equals("ollama", StringComparison.OrdinalIgnoreCase) &&
                IsQwen25CoderInstructQ4Km(model))
            {
                return Profiles["ollama/qwen2.5-coder-instruct-q4_k_m"];
            }

            if (provider.Equals("ollama", StringComparison.OrdinalIgnoreCase) &&
                IsQwen25CoderFamily(model))
            {
                return Profiles["ollama/qwen2.5-coder"];
            }

            var key = $"{provider}/default";
            return Profiles.TryGetValue(key, out var profile)
                ? profile
                : Profiles["ollama/default"];
        }

        public static LlmRuntimePolicy ResolvePolicy(string provider, string? model = null)
        {
            if (provider.Equals("ollama", StringComparison.OrdinalIgnoreCase) &&
                IsQwen25CoderFamily(model))
            {
                return OllamaRelaxedPolicy;
            }
            if (provider.Equals("gemini", StringComparison.OrdinalIgnoreCase))
                return GeminiStandardPolicy;

            return LlmRuntimePolicy.Default;
        }

        private static bool IsQwen25CoderFamily(string? model)
        {
            if (string.IsNullOrWhiteSpace(model))
                return false;

            return model.Contains("qwen2.5-coder", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsQwen25CoderInstructQ4Km(string? model)
        {
            if (string.IsNullOrWhiteSpace(model))
                return false;

            return model.Contains("qwen2.5-coder:7b-instruct-q4_k_m", StringComparison.OrdinalIgnoreCase);
        }
    }
}
