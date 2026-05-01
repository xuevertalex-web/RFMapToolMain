using LocalCursorAgent.LLM.Runtime;

namespace LocalCursorAgent.Core
{
    internal static class RuntimeTuningResolver
    {
        public static string ResolveRuntimeProfileId(string? provider, string? model)
        {
            if (string.IsNullOrWhiteSpace(provider))
                return string.Empty;

            try
            {
                return LlmProfiles.Resolve(provider, model).ProfileId;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string ResolveRuntimeEndpoint(string? provider)
        {
            if (string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
                return Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT")?.Trim() is { Length: > 0 } endpoint ? endpoint : "http://localhost:11434";

            return string.Empty;
        }

        public static string ResolveConfiguredContextWindow(string? provider)
        {
            if (!string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var numCtx = Environment.GetEnvironmentVariable("OLLAMA_NUM_CTX")?.Trim();
            return string.IsNullOrWhiteSpace(numCtx) ? string.Empty : numCtx;
        }

        public static string ResolveConfiguredGpuOffloadOptions(string? provider)
        {
            if (!string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var numGpu = Environment.GetEnvironmentVariable("OLLAMA_NUM_GPU")?.Trim();
            var gpuLayers = Environment.GetEnvironmentVariable("OLLAMA_GPU_LAYERS")?.Trim();
            var options = new List<string>();
            if (!string.IsNullOrWhiteSpace(numGpu))
                options.Add($"num_gpu={numGpu}");
            if (!string.IsNullOrWhiteSpace(gpuLayers))
                options.Add($"gpu_layers={gpuLayers}");
            return string.Join(";", options);
        }

        public static string ResolveRuntimeTuningProfile(string? provider, string? model)
        {
            if (!string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            if (string.IsNullOrWhiteSpace(model))
                return string.Empty;
            return model.Contains("qwen2.5-coder:7b-instruct-q4_k_m", StringComparison.OrdinalIgnoreCase)
                ? "7b-quality-gpu-tuned"
                : model.Contains("qwen2.5-coder:3b-instruct-q4_k_m", StringComparison.OrdinalIgnoreCase)
                    ? "3b-fast"
                    : string.Empty;
        }

        public static string ResolveRuntimeTuningOptions(string? provider, string? model)
        {
            if (!string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(model))
                return string.Empty;
            if (!model.Contains("qwen2.5-coder:7b-instruct-q4_k_m", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            var numCtx = Environment.GetEnvironmentVariable("LOCALCURSOR_OLLAMA_7B_NUM_CTX")?.Trim();
            var numGpu = Environment.GetEnvironmentVariable("LOCALCURSOR_OLLAMA_7B_NUM_GPU")?.Trim();
            var gpuLayers = Environment.GetEnvironmentVariable("LOCALCURSOR_OLLAMA_7B_GPU_LAYERS")?.Trim();
            var temperature = Environment.GetEnvironmentVariable("LOCALCURSOR_OLLAMA_7B_TEMPERATURE")?.Trim();
            var options = new List<string>
            {
                $"num_ctx={(string.IsNullOrWhiteSpace(numCtx) ? "8192" : numCtx)}",
                $"num_gpu={(string.IsNullOrWhiteSpace(numGpu) ? "1" : numGpu)}",
                $"gpu_layers={(string.IsNullOrWhiteSpace(gpuLayers) ? "32" : gpuLayers)}",
                $"temperature={(string.IsNullOrWhiteSpace(temperature) ? "0.2" : temperature)}"
            };
            return string.Join(";", options);
        }

        public static string ResolveRuntimeTuningSource(string? provider, string? model)
        {
            if (!string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(model))
                return string.Empty;
            if (!model.Contains("qwen2.5-coder:7b-instruct-q4_k_m", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            var hasOverride =
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOCALCURSOR_OLLAMA_7B_NUM_CTX")) ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOCALCURSOR_OLLAMA_7B_NUM_GPU")) ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOCALCURSOR_OLLAMA_7B_GPU_LAYERS")) ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOCALCURSOR_OLLAMA_7B_TEMPERATURE"));
            return hasOverride ? "env" : "default";
        }

        public static bool ResolveRuntimeTuningApplied(string? provider, string? model)
        {
            return !string.IsNullOrWhiteSpace(ResolveRuntimeTuningProfile(provider, model));
        }

        public static string[] ResolveRuntimeTuningWarnings(string? provider, string? model)
        {
            var warnings = new List<string>();
            if (!string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(model))
                return warnings.ToArray();
            if (!model.Contains("qwen2.5-coder:7b-instruct-q4_k_m", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add("MODEL_NOT_7B_TUNED");
                return warnings.ToArray();
            }

            var gpuLayersRaw = Environment.GetEnvironmentVariable("LOCALCURSOR_OLLAMA_7B_GPU_LAYERS")?.Trim();
            var ctxRaw = Environment.GetEnvironmentVariable("LOCALCURSOR_OLLAMA_7B_NUM_CTX")?.Trim();
            if (string.IsNullOrWhiteSpace(gpuLayersRaw))
            {
                warnings.Add("GPU_LAYERS_NOT_SET");
            }

            var effectiveCtx = 8192;
            if (int.TryParse(ctxRaw, out var parsedCtx) && parsedCtx > 0)
                effectiveCtx = parsedCtx;
            if (effectiveCtx > 8192)
            {
                warnings.Add("CTX_TOO_HIGH_FOR_4GB");
            }

            return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}
