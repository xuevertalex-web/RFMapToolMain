using LocalCursorAgent.LLM;
using LocalCursorAgent.Security;

internal static class ProgramRuntimeHelpers
{
    public static string DescribeAccessMode(AgentAccessMode accessMode) =>
        AccessModeDescriptionResolver.Describe(accessMode);

    public static ILLMClient CreateLlmClient(string? providerOverride, string? ollamaModelOverride, string appRoot)
    {
        return LocalCursorAgent.LLM.Runtime.LlmRuntimeFactory.Create(providerOverride, ollamaModelOverride, appRoot);
    }
}
