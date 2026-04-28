namespace LocalCursorAgent.Security;

public static class AgentExecutionProfile
{
    private const string UnrestrictedSandboxEnv = "LOCALCURSOR_UNRESTRICTED_SANDBOX";

    public static bool IsUnrestrictedInsideSandbox(AgentSessionContext? session)
    {
        if (session?.AccessMode != AgentAccessMode.WorkspaceFullAccess)
            return false;

        var raw = Environment.GetEnvironmentVariable(UnrestrictedSandboxEnv);
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        return raw.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               raw.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}
