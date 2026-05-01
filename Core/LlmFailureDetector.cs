using LocalCursorAgent.LLM.Runtime;

namespace LocalCursorAgent.Core
{
    internal static class LlmFailureDetector
    {
        public static bool IsHardLlmFailureResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return false;

            var fallbackMetadata = new LlmProviderMetadata("legacy", string.Empty, "legacy-classifier");
            var fallbackProfile = LlmProfiles.Resolve("ollama");
            var classified = LlmRuntimeClassifier.Classify(response, fallbackMetadata, fallbackProfile, LlmRuntimePolicy.Default);
            return classified.IsFailure;
        }
    }
}
