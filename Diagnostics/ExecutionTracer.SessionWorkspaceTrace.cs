using System.Collections.Generic;
using System.Linq;
using LocalCursorAgent.Security;

namespace LocalCursorAgent.Diagnostics;

public partial class ExecutionTracer
{
    public void LogWorkspaceResolution(string? seedPath, WorkspaceResolutionResult resolution, string runtimeRoot)
    {
        _lastWorkspaceResolution = new WorkspaceResolutionSnapshot
        {
            SeedPath = seedPath ?? string.Empty,
            RuntimeRoot = runtimeRoot,
            Success = resolution.Success,
            Reason = resolution.Reason.ToString(),
            ReasonCode = resolution.ReasonCode,
            ReasonCodeName = resolution.ReasonCodeName,
            Message = resolution.Message,
            WorkspaceRoot = resolution.WorkspaceRoot ?? string.Empty,
            Source = resolution.Source ?? string.Empty
        };

        LogEvent("WorkspaceResolution", "Workspace resolved", new Dictionary<string, object>
        {
            { "SeedPath", seedPath ?? string.Empty },
            { "RuntimeRoot", runtimeRoot },
            { "Success", resolution.Success },
            { "ReasonCode", resolution.ReasonCode },
            { "ReasonCodeName", resolution.ReasonCodeName },
            { "Reason", resolution.Reason.ToString() },
            { "Message", resolution.Message },
            { "WorkspaceRoot", resolution.WorkspaceRoot ?? string.Empty },
            { "Source", resolution.Source ?? string.Empty }
        });
    }

    public void LogSessionHeader(AgentSessionContext session, IReadOnlyList<string> protectedRoots)
    {
        _lastSessionHeader = new SessionHeader
        {
            SessionId = session.SessionId,
            RuntimeRoot = session.RuntimeRoot,
            WorkspaceRoot = session.ActiveWorkspaceRoot,
            AccessMode = session.AccessMode.ToString(),
            ProtectedRoots = protectedRoots.ToArray()
        };

        LogEvent("SessionHeader", "Agent session initialized", new Dictionary<string, object>
        {
            { "SessionId", session.SessionId },
            { "RuntimeRoot", session.RuntimeRoot },
            { "WorkspaceRoot", session.ActiveWorkspaceRoot },
            { "AccessMode", session.AccessMode.ToString() },
            { "ProtectedRoots", protectedRoots.ToArray() }
        });
    }
}
