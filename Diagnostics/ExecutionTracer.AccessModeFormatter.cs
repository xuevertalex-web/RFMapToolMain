using LocalCursorAgent.Security;

namespace LocalCursorAgent.Diagnostics;

internal static class ExecutionTracerAccessModeFormatter
{
    public static string DescribeAccessMode(string accessMode) => accessMode switch
    {
        nameof(AgentAccessMode.ReadOnly) => "Analysis only; no writes or destructive actions.",
        nameof(AgentAccessMode.WorkspaceWrite) => "Write/patch allowed inside workspace; destructive actions denied.",
        nameof(AgentAccessMode.WorkspaceFullAccess) => "Full engineering access inside workspace; runtime and protected paths remain denied.",
        _ => "Unknown access mode."
    };
}
