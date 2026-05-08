using LocalCursorAgent.LLM;
using LocalCursorAgent.Security;

internal static class ProgramRuntimeHelpers
{
    public static string DescribeAccessMode(AgentAccessMode accessMode) => accessMode switch
    {
        AgentAccessMode.ReadOnly => "Analysis only; no writes or destructive actions.",
        AgentAccessMode.WorkspaceWrite => "Write/patch allowed inside workspace; destructive actions denied.",
        AgentAccessMode.WorkspaceFullAccess => "Full engineering access inside workspace; runtime and protected paths remain denied.",
        _ => "Unknown access mode."
    };

    public static ILLMClient CreateLlmClient(string? providerOverride, string? ollamaModelOverride, string appRoot)
    {
        return LocalCursorAgent.LLM.Runtime.LlmRuntimeFactory.Create(providerOverride, ollamaModelOverride, appRoot);
    }
}
