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
                ExpectedLatencyClass: "medium",
                StreamingSupported: false,
                StructuredOutputReliability: "medium",
                CodeStrength: "medium",
                ParsingStrictness: "balanced",
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
                RetryPreference: "provider-fallback",
                PreferFallbackOnHardFailure: true)
        };

        public static LlmRuntimeProfile Resolve(string provider)
        {
            var key = $"{provider}/default";
            return Profiles.TryGetValue(key, out var profile)
                ? profile
                : Profiles["ollama/default"];
        }
    }
}
