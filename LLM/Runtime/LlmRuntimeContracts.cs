namespace LocalCursorAgent.LLM.Runtime
{
    public enum LlmRuntimeStatus
    {
        Success,
        PartialOutput,
        ModelTimeout,
        LlmRequestFailed,
        ModelOutputEmpty,
        ModelOutputParseFailed,
        ProviderUnavailable,
        UnsupportedCapability
    }

    public enum LlmTimeoutKind
    {
        None,
        ConnectStart,
        FirstResponse,
        Stall,
        OverallSafetyBudget
    }

    public sealed record LlmProviderMetadata(
        string Provider,
        string Model,
        string Adapter);

    public sealed record LlmRuntimeProfile(
        string ProfileId,
        string Provider,
        string ContextClass,
        string ExpectedLatencyClass,
        bool StreamingSupported,
        string StructuredOutputReliability,
        string CodeStrength,
        string ParsingStrictness,
        string RetryPreference,
        bool PreferFallbackOnHardFailure);

    public sealed record LlmRuntimePolicy(
        TimeSpan ConnectStartTimeout,
        TimeSpan FirstResponseTimeout,
        TimeSpan StallTimeout,
        TimeSpan OverallSafetyBudget)
    {
        public static LlmRuntimePolicy Default { get; } = new(
            ConnectStartTimeout: TimeSpan.FromSeconds(15),
            FirstResponseTimeout: TimeSpan.FromSeconds(90),
            StallTimeout: TimeSpan.FromSeconds(60),
            OverallSafetyBudget: TimeSpan.FromMinutes(3));
    }

    public sealed record LlmRuntimeResult(
        string Completion,
        LlmRuntimeStatus Status,
        LlmProviderMetadata Metadata,
        LlmRuntimeProfile Profile,
        LlmRuntimePolicy Policy,
        bool IsUsable,
        bool IsFailure,
        LlmTimeoutKind TimeoutKind,
        string? FailureMessage);

    public interface ILlmRuntimeClient
    {
        LlmProviderMetadata Metadata { get; }
        LlmRuntimeProfile Profile { get; }
        LlmRuntimePolicy Policy { get; }
        Task<LlmRuntimeResult> GenerateNormalized(string prompt, CancellationToken cancellationToken = default);
    }

    public interface ILlmProviderAdapter
    {
        LlmProviderMetadata Metadata { get; }
        Task<string> Generate(string prompt, CancellationToken cancellationToken = default);
        Task<bool> IsAvailable(CancellationToken cancellationToken = default);
    }
}
