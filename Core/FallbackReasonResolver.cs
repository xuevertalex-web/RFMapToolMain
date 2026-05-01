using LocalCursorAgent.LLM.Runtime;

namespace LocalCursorAgent.Core
{
    internal static class FallbackReasonResolver
    {
        public static string Resolve(LlmRuntimeResult? runtimeResult, string response, Func<string, bool> isModelTimeoutResponse)
        {
            if (runtimeResult is not null)
            {
                return runtimeResult.Status switch
                {
                    LlmRuntimeStatus.ModelTimeout => "MODEL_TIMEOUT",
                    LlmRuntimeStatus.ProviderUnavailable => "PROVIDER_UNAVAILABLE",
                    LlmRuntimeStatus.UnsupportedCapability => "UNSUPPORTED_CAPABILITY",
                    _ => "LLM_REQUEST_FAILED"
                };
            }

            return isModelTimeoutResponse(response) ? "MODEL_TIMEOUT" : "LLM_REQUEST_FAILED";
        }
    }
}
