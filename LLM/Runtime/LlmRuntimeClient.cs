using LocalCursorAgent.LLM;

namespace LocalCursorAgent.LLM.Runtime
{
    public sealed class LlmRuntimeClient : ILLMClient, ILlmRuntimeClient
    {
        private readonly ILlmProviderAdapter _adapter;

        public LlmRuntimeClient(
            ILlmProviderAdapter adapter,
            LlmRuntimeProfile profile,
            LlmRuntimePolicy? policy = null)
        {
            _adapter = adapter;
            Profile = profile;
            Policy = policy ?? LlmRuntimePolicy.Default;
        }

        public LlmProviderMetadata Metadata => _adapter.Metadata;
        public LlmRuntimeProfile Profile { get; }
        public LlmRuntimePolicy Policy { get; }

        public async Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
        {
            var result = await GenerateNormalized(prompt, cancellationToken);
            return result.Completion;
        }

        public Task<bool> IsAvailable(CancellationToken cancellationToken = default)
        {
            return _adapter.IsAvailable(cancellationToken);
        }

        public async Task<LlmRuntimeResult> GenerateNormalized(string prompt, CancellationToken cancellationToken = default)
        {
            var response = await _adapter.Generate(prompt, cancellationToken);
            return LlmRuntimeClassifier.Classify(response, Metadata, Profile, Policy);
        }
    }
}
