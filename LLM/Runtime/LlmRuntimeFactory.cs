using System.Text.Json;
using LocalCursorAgent.LLM;

namespace LocalCursorAgent.LLM.Runtime
{
    public static class LlmRuntimeFactory
    {
        public static ILLMClient Create(string? providerOverride, string? ollamaModelOverride, string appRoot)
        {
            var provider = NormalizeProvider(providerOverride ?? Environment.GetEnvironmentVariable("LOCALCURSOR_LLM_PROVIDER"));
            var preferOpenAi = provider is "openai" or "chatgpt" or "hybrid" or "";
            var preferOllama = provider is "local" or "ollama";

            var openAiClient = TryCreateOpenAiClient(appRoot);
            var ollamaClient = CreateOllamaClient(ollamaModelOverride);

            if (preferOllama && openAiClient is not null)
                return new FallbackLLMClient(ollamaClient, openAiClient);

            if (preferOpenAi && openAiClient is not null)
                return new FallbackLLMClient(openAiClient, ollamaClient);

            return ollamaClient;
        }

        private static string NormalizeProvider(string? provider)
        {
            return (provider ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static ILLMClient CreateOllamaClient(string? ollamaModelOverride)
        {
            var endpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT")?.Trim();
            var modelName = ollamaModelOverride ?? Environment.GetEnvironmentVariable("OLLAMA_MODEL")?.Trim();
            var model = string.IsNullOrWhiteSpace(modelName) ? "qwen2.5-coder:7b" : modelName;

            var adapter = new OllamaProviderAdapter(
                string.IsNullOrWhiteSpace(endpoint) ? "http://localhost:11434" : endpoint,
                model);
            var profile = LlmProfiles.Resolve("ollama", model);
            var policy = LlmProfiles.ResolvePolicy("ollama", model);
            return new LlmRuntimeClient(adapter, profile, policy);
        }

        private static ILLMClient? TryCreateOpenAiClient(string appRoot)
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim();
            apiKey = string.IsNullOrWhiteSpace(apiKey) ? LoadOpenAiApiKeyFallback(appRoot) : apiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                return null;

            var model = Environment.GetEnvironmentVariable("OPENAI_MODEL")?.Trim();
            model = string.IsNullOrWhiteSpace(model) ? "gpt-4.1-mini" : model;
            var adapter = new OpenAiProviderAdapter(apiKey, model);
            var profile = LlmProfiles.Resolve("openai", model);
            var policy = LlmProfiles.ResolvePolicy("openai", model);
            return new LlmRuntimeClient(adapter, profile, policy);
        }

        private static string LoadOpenAiApiKeyFallback(string appRoot)
        {
            var candidates = new[]
            {
                Path.Combine(appRoot, "localcursoragent.secrets.json"),
                Path.Combine(AppContext.BaseDirectory, "localcursoragent.secrets.json")
            };

            foreach (var candidate in candidates)
            {
                if (!File.Exists(candidate))
                    continue;

                try
                {
                    var json = File.ReadAllText(candidate);
                    var secrets = JsonSerializer.Deserialize<LocalCursorAgentSecrets>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (!string.IsNullOrWhiteSpace(secrets?.OpenAiApiKey))
                        return secrets.OpenAiApiKey.Trim();
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private sealed class LocalCursorAgentSecrets
        {
            public string? OpenAiApiKey { get; set; }
        }
    }
}
