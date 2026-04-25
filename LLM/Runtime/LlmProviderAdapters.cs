using LocalCursorAgent.LLM;

namespace LocalCursorAgent.LLM.Runtime
{
    public sealed class OllamaProviderAdapter : ILlmProviderAdapter
    {
        private readonly OllamaClient _client;

        public OllamaProviderAdapter(string endpoint, string model)
        {
            _client = new OllamaClient(endpoint, model);
            Metadata = new LlmProviderMetadata("ollama", model, nameof(OllamaProviderAdapter));
        }

        public LlmProviderMetadata Metadata { get; }

        public Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
            => _client.Generate(prompt, cancellationToken);

        public Task<bool> IsAvailable(CancellationToken cancellationToken = default)
            => _client.IsAvailable(cancellationToken);
    }

    public sealed class OpenAiProviderAdapter : ILlmProviderAdapter
    {
        private readonly OpenAIChatClient _client;

        public OpenAiProviderAdapter(string apiKey, string model)
        {
            _client = new OpenAIChatClient(apiKey, model);
            Metadata = new LlmProviderMetadata("openai", model, nameof(OpenAiProviderAdapter));
        }

        public LlmProviderMetadata Metadata { get; }

        public Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
            => _client.Generate(prompt, cancellationToken);

        public Task<bool> IsAvailable(CancellationToken cancellationToken = default)
            => _client.IsAvailable(cancellationToken);
    }

    public sealed class GeminiProviderAdapter : ILlmProviderAdapter
    {
        private readonly GeminiChatClient _client;

        public GeminiProviderAdapter(string apiKey, string model)
        {
            _client = new GeminiChatClient(apiKey, model);
            Metadata = new LlmProviderMetadata("gemini", model, nameof(GeminiProviderAdapter));
        }

        public LlmProviderMetadata Metadata { get; }

        public Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
            => _client.Generate(prompt, cancellationToken);

        public Task<bool> IsAvailable(CancellationToken cancellationToken = default)
            => _client.IsAvailable(cancellationToken);
    }
}
