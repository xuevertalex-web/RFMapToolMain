using LocalCursorAgent.Security;

namespace LocalCursorAgent.Core
{
    internal static class StartupStateFormatter
    {
        public static string BuildStartupStateBlock(AgentSessionContext? sessionContext, WorkspaceResolutionResult? workspaceResolution)
        {
            if (sessionContext is null && workspaceResolution is null)
                return string.Empty;

            var lines = new List<string>();

            if (workspaceResolution is not null)
            {
                lines.Add("WORKSPACE RESOLUTION:");
                lines.Add($"- Success: {workspaceResolution.Success}");
                lines.Add($"- Reason: {workspaceResolution.ReasonCodeName} / {workspaceResolution.ReasonCode}");
                lines.Add($"- Source: {workspaceResolution.Source ?? string.Empty}");
                lines.Add($"- Workspace root: {workspaceResolution.WorkspaceRoot ?? string.Empty}");
                lines.Add($"- Message: {workspaceResolution.Message}");
            }

            if (sessionContext is not null)
            {
                if (lines.Count > 0)
                    lines.Add(string.Empty);

                lines.Add("SESSION STARTUP:");
                lines.Add($"- Session id: {sessionContext.SessionId}");
                lines.Add($"- Runtime root: {sessionContext.RuntimeRoot}");
                lines.Add($"- Active workspace root: {sessionContext.ActiveWorkspaceRoot}");
                lines.Add($"- Access mode: {sessionContext.AccessMode}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
