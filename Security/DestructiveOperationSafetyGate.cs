using System.Security.Cryptography;
using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Security;

public sealed partial class DestructiveOperationSafetyGate
{
    private readonly AgentSessionContext _session;
    private readonly PermissionGuard _permissionGuard;
    private readonly ExecutionTracer? _tracer;
    private readonly string _snapshotRoot;
    private readonly Func<DestructiveTraceRecord, bool>? _failureHook;

    public DestructiveOperationSafetyGate(
        AgentSessionContext session,
        PermissionGuard permissionGuard,
        ExecutionTracer? tracer = null,
        Func<DestructiveTraceRecord, bool>? failureHook = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _permissionGuard = permissionGuard ?? throw new ArgumentNullException(nameof(permissionGuard));
        _tracer = tracer;
        _failureHook = failureHook;
        _snapshotRoot = Path.Combine(_session.RuntimeRoot, "snapshots", _session.SessionId);
        Directory.CreateDirectory(_snapshotRoot);
    }

}
