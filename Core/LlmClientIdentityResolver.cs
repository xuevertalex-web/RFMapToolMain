using LocalCursorAgent.LLM;
using LocalCursorAgent.LLM.Runtime;

namespace LocalCursorAgent.Core
{
    internal static class LlmClientIdentityResolver
    {
        public static string ResolveProviderName(ILLMClient llmClient, LlmProviderMetadata? metadata = null)
        {
            if (metadata is not null && !string.IsNullOrWhiteSpace(metadata.Provider))
                return metadata.Provider;

            if (llmClient == null)
                return string.Empty;

            var typeName = llmClient.GetType().Name;
            if (typeName.EndsWith("Client", StringComparison.OrdinalIgnoreCase))
                typeName = typeName[..^"Client".Length];
            return typeName;
        }

        public static string ResolveModelName(ILLMClient llmClient, LlmProviderMetadata? metadata = null)
        {
            if (metadata is not null && !string.IsNullOrWhiteSpace(metadata.Model))
                return metadata.Model;

            if (llmClient == null)
                return string.Empty;

            var field = llmClient.GetType().GetField("_model", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field?.GetValue(llmClient) is string model && !string.IsNullOrWhiteSpace(model))
                return model;

            return string.Empty;
        }
    }
}
