namespace LocalCursorAgent.Security;

internal static class AccessModeDescriptionResolver
{
    public static string Describe(AgentAccessMode accessMode) => accessMode switch
    {
        AgentAccessMode.ReadOnly => "Analysis only; no writes or destructive actions.",
        AgentAccessMode.WorkspaceWrite => "Write/patch allowed inside workspace; destructive actions denied.",
        AgentAccessMode.WorkspaceFullAccess => "Full engineering access inside workspace; runtime and protected paths remain denied.",
        _ => "Unknown access mode."
    };

    public static string Describe(string? accessModeText)
    {
        if (Enum.TryParse<AgentAccessMode>(accessModeText, ignoreCase: true, out var mode))
            return Describe(mode);

        return "Unknown access mode.";
    }
}
