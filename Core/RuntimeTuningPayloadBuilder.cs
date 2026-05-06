namespace LocalCursorAgent.Core
{
    internal static class RuntimeTuningPayloadBuilder
    {
        internal static RuntimeTuningPayloadValues Build(string? provider, string? model)
        {
            return new RuntimeTuningPayloadValues
            {
                RuntimeProfile = RuntimeTuningResolver.ResolveRuntimeProfileId(provider, model),
                RuntimeEndpoint = RuntimeTuningResolver.ResolveRuntimeEndpoint(provider),
                ConfiguredContextWindow = RuntimeTuningResolver.ResolveConfiguredContextWindow(provider),
                ConfiguredGpuOffloadOptions = RuntimeTuningResolver.ResolveConfiguredGpuOffloadOptions(provider),
                RuntimeTuningProfile = RuntimeTuningResolver.ResolveRuntimeTuningProfile(provider, model),
                RuntimeTuningOptions = RuntimeTuningResolver.ResolveRuntimeTuningOptions(provider, model),
                RuntimeTuningSource = RuntimeTuningResolver.ResolveRuntimeTuningSource(provider, model),
                RuntimeTuningApplied = RuntimeTuningResolver.ResolveRuntimeTuningApplied(provider, model),
                RuntimeTuningWarnings = RuntimeTuningResolver.ResolveRuntimeTuningWarnings(provider, model)
            };
        }
    }

    internal sealed class RuntimeTuningPayloadValues
    {
        public string RuntimeProfile { get; init; } = string.Empty;
        public string RuntimeEndpoint { get; init; } = string.Empty;
        public string ConfiguredContextWindow { get; init; } = string.Empty;
        public string ConfiguredGpuOffloadOptions { get; init; } = string.Empty;
        public string RuntimeTuningProfile { get; init; } = string.Empty;
        public string RuntimeTuningOptions { get; init; } = string.Empty;
        public string RuntimeTuningSource { get; init; } = string.Empty;
        public bool RuntimeTuningApplied { get; init; }
        public string[] RuntimeTuningWarnings { get; init; } = Array.Empty<string>();
    }
}
