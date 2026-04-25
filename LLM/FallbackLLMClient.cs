namespace LocalCursorAgent.LLM
{
    public sealed class FallbackLLMClient : ILLMClient
    {
        private readonly ILLMClient _primary;
        private readonly ILLMClient _fallback;

        public FallbackLLMClient(ILLMClient primary, ILLMClient fallback)
        {
            _primary = primary;
            _fallback = fallback;
        }

        public async Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
        {
            var primaryResult = await _primary.Generate(prompt, cancellationToken);
            if (!IsHardFailure(primaryResult))
                return primaryResult;

            var fallbackResult = await _fallback.Generate(prompt, cancellationToken);
            return fallbackResult;
        }

        public async Task<bool> IsAvailable(CancellationToken cancellationToken = default)
        {
            return await _primary.IsAvailable(cancellationToken) || await _fallback.IsAvailable(cancellationToken);
        }

        private static bool IsHardFailure(string result)
        {
            return result.StartsWith("Error:", StringComparison.OrdinalIgnoreCase);
        }
    }
}
