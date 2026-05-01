namespace LocalCursorAgent.Core
{
    internal static class WorkspacePolicyFormatter
    {
        public static string BuildPolicyBlock(AgentSessionContext? sessionContext)
        {
            if (sessionContext is null)
                return string.Empty;

            var mode = sessionContext.AccessMode.ToString();
            return $@"WORKSPACE POLICY:
- Active workspace root: {sessionContext.ActiveWorkspaceRoot}
- Runtime root: {sessionContext.RuntimeRoot}
- Access mode: {mode}
- ReadOnly: read/analysis only; no write, delete, rename, move.
- WorkspaceWrite: patch/write/create allowed inside workspace; destructive ops denied.
- WorkspaceFullAccess: destructive ops allowed only inside workspace; runtime/protected paths remain denied.
- Never target the runtime root or protected paths.";
        }
    }
}
