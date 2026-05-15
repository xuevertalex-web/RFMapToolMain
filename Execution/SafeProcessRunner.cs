using LocalCursorAgent.Security;
using System.Diagnostics;
using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Execution;

public sealed class SafeProcessRunner
{
    private static readonly string[] DefaultDotnetBuildArgs =
    {
        "build",
        "/nodeReuse:false",
        "/p:UseSharedCompilation=false"
    };

    private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet",
        "git",
        "npm"
    };
    private static readonly string[] ForbiddenShellTokens = { "&&", "|", ">", "<", ";" };

    private readonly AgentSessionContext _session;
    private readonly PermissionGuard _permissionGuard;
    private readonly ExecutionTracer? _tracer;

    public SafeProcessRunner(AgentSessionContext session, PermissionGuard permissionGuard, ExecutionTracer? tracer = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _permissionGuard = permissionGuard ?? throw new ArgumentNullException(nameof(permissionGuard));
        _tracer = tracer;
    }

    public async Task<SafeProcessResult> RunAsync(
        SafeProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Command) || !AllowedCommands.Contains(request.Command))
        {
            _tracer?.LogActionEvent("ProcessSpawn", "SafeProcessRunner", ExecutionTracer.ActionLogLevel.Warning, "rejected", PermissionReasonCodes.ToolDeniedByPolicy, new Dictionary<string, object?>
            {
                { "command", request.Command },
                { "working_directory", request.WorkingDirectory },
                { "args", request.Args?.ToArray() ?? Array.Empty<string>() }
            });
            return SafeProcessResult.InvalidCommand($"Command '{request.Command}' is not allowed", request.Command, request.WorkingDirectory);
        }
        if (ContainsForbiddenShellTokens(request.Command) || (request.Args?.Any(ContainsForbiddenShellTokens) ?? false))
        {
            return SafeProcessResult.BlockedProcessExecution("Shell chaining/redirection tokens are not allowed", request.Command, request.WorkingDirectory);
        }

        var workingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
            ? (_session.ExecutionWorkspaceRoot ?? _session.ActiveWorkspaceRoot)
            : request.WorkingDirectory!;
        if (!IsWithinExecutionRoot(workingDirectory))
        {
            return SafeProcessResult.BlockedProcessExecution("Working directory must stay within execution workspace", request.Command, workingDirectory);
        }
        var action = new ToolAction
        {
            Kind = request.Kind,
            WorkingDirectory = workingDirectory,
            CommandExecutable = request.Command,
            CommandArgs = request.Args?.ToArray(),
            Payload = string.Join(" ", request.Args ?? Array.Empty<string>())
        };

        var decision = _permissionGuard.Evaluate(_session, action);
        _tracer?.LogPermissionDecision(_session, "process", action, decision);
        if (!decision.Allowed)
        {
            _tracer?.LogActionEvent("ProcessSpawn", "SafeProcessRunner", ExecutionTracer.ActionLogLevel.Warning, "denied", decision.ReasonCodeString, new Dictionary<string, object?>
            {
                { "command", request.Command },
                { "working_directory", request.WorkingDirectory },
                { "args", request.Args?.ToArray() ?? Array.Empty<string>() }
            });
            return SafeProcessResult.Denied(
                decision.ReasonCode,
                decision.Message,
                request.Command,
                request.WorkingDirectory);
        }

        if (!Directory.Exists(workingDirectory))
        {
            _tracer?.LogActionEvent("ProcessSpawn", "SafeProcessRunner", ExecutionTracer.ActionLogLevel.Warning, "denied", PermissionReasonCodes.InvalidWorkingDirectory, new Dictionary<string, object?>
            {
                { "command", request.Command },
                { "working_directory", workingDirectory },
                { "args", request.Args?.ToArray() ?? Array.Empty<string>() }
            });
            return SafeProcessResult.Denied(
                PermissionReasonCode.InvalidWorkingDirectory,
                $"Working directory not found: {workingDirectory}",
                request.Command,
                workingDirectory);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = request.Command,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in GetNormalizedArgs(request))
            startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };
        var startedAt = DateTime.UtcNow;
        _tracer?.LogActionEvent("ProcessSpawn", "SafeProcessRunner", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
        {
            { "command", request.Command },
            { "args", request.Args?.ToArray() ?? Array.Empty<string>() },
            { "working_directory", workingDirectory }
        });
        try
        {
            if (!process.Start())
            {
                _tracer?.LogActionEvent("ProcessSpawn", "SafeProcessRunner", ExecutionTracer.ActionLogLevel.Error, "failed", PermissionReasonCodes.ToolDeniedByPolicy, new Dictionary<string, object?>
                {
                    { "command", request.Command },
                    { "working_directory", workingDirectory }
                });
                return SafeProcessResult.Failed("Failed to start process", request.Command, workingDirectory);
            }
        }
        catch (Exception ex)
        {
            _tracer?.LogActionEvent("ProcessSpawn", "SafeProcessRunner", ExecutionTracer.ActionLogLevel.Error, "failed", PermissionReasonCodes.ToolDeniedByPolicy, new Dictionary<string, object?>
            {
                { "command", request.Command },
                { "working_directory", workingDirectory },
                { "message", ex.Message }
            });
            return SafeProcessResult.Failed(ex.Message, request.Command, workingDirectory);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        var waitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = request.Timeout.HasValue && request.Timeout.Value > TimeSpan.Zero
            ? Task.Delay(request.Timeout.Value, cancellationToken)
            : Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        var timedOut = false;
        var canceled = false;
        try
        {
            var completed = await Task.WhenAny(waitTask, timeoutTask);
            timedOut = completed == timeoutTask;

            if (timedOut)
            {
                TerminateProcessTree(process);
            }
            else
            {
                await waitTask;
            }
        }
        catch (OperationCanceledException)
        {
            canceled = true;
            TerminateProcessTree(process);
        }

        if (timedOut || canceled)
        {
            try
            {
                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch
            {
                // Best effort after forced termination.
            }
        }

        await Task.WhenAll(stdoutTask, stderrTask);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        _tracer?.LogActionEvent("ProcessSpawn", "SafeProcessRunner", timedOut || canceled || process.ExitCode != 0 ? ExecutionTracer.ActionLogLevel.Warning : ExecutionTracer.ActionLogLevel.Info, timedOut ? "timed_out" : (canceled ? "canceled" : "completed"), timedOut ? "PROCESS_TIMEOUT" : (canceled ? "PROCESS_CANCELED" : PermissionReasonCodes.Allowed), new Dictionary<string, object?>
        {
            { "command", request.Command },
            { "args", GetNormalizedArgs(request) },
            { "working_directory", workingDirectory },
            { "exit_code", timedOut ? -1 : process.ExitCode },
            { "canceled", canceled },
            { "stdout_size", stdout.Length },
            { "stderr_size", stderr.Length }
        }, durationMs: (long)(DateTime.UtcNow - startedAt).TotalMilliseconds);

        return new SafeProcessResult
        {
            Success = !timedOut && !canceled && process.ExitCode == 0,
            TimedOut = timedOut,
            ExitCode = timedOut || canceled ? -1 : process.ExitCode,
            StdOut = stdout,
            StdErr = stderr,
            Command = request.Command,
            Arguments = string.Join(" ", GetNormalizedArgs(request)),
            WorkingDirectory = workingDirectory,
            ReasonCode = timedOut ? "PROCESS_TIMEOUT" : (canceled ? "PROCESS_CANCELED" : PermissionReasonCodes.Allowed),
            Message = canceled ? "Process execution was canceled" : string.Empty
        };
    }

    public static IReadOnlyList<string> GetDefaultBuildArgs() => DefaultDotnetBuildArgs;

    private static IReadOnlyList<string> GetNormalizedArgs(SafeProcessRequest request)
    {
        var args = request.Args?.ToArray() ?? Array.Empty<string>();
        if (string.Equals(request.Command, "dotnet", StringComparison.OrdinalIgnoreCase) &&
            request.Kind == ToolActionKind.Build &&
            args.Length == 1 &&
            string.Equals(args[0], "build", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultDotnetBuildArgs;
        }

        return args;
    }

    private static void TerminateProcessTree(Process process)
    {
        if (process.HasExited)
            return;

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }
    }

    private bool IsWithinExecutionRoot(string path)
    {
        var root = _session.ExecutionWorkspaceRoot ?? _session.ActiveWorkspaceRoot;
        return CanonicalPathPolicy.IsCanonicallyContained(root, path);
    }

    private static bool ContainsForbiddenShellTokens(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return ForbiddenShellTokens.Any(token => value.Contains(token, StringComparison.Ordinal));
    }
}

public sealed class SafeProcessRequest
{
    public required ToolActionKind Kind { get; init; }
    public required string Command { get; init; }
    public IReadOnlyList<string>? Args { get; init; }
    public required string WorkingDirectory { get; init; }
    public TimeSpan? Timeout { get; init; }
}

public sealed class SafeProcessResult
{
    public bool Success { get; init; }
    public bool TimedOut { get; init; }
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = string.Empty;
    public string StdErr { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
    public string ReasonCode { get; init; } = PermissionReasonCodes.Allowed;
    public string Message { get; init; } = string.Empty;

    public static SafeProcessResult Denied(PermissionReasonCode reason, string message, string command, string workingDirectory) => new()
    {
        Success = false,
        TimedOut = false,
        ExitCode = -1,
        ReasonCode = reason switch
        {
            PermissionReasonCode.PathOutsideWorkspace => PermissionReasonCodes.AccessDeniedOutsideWorkspace,
            PermissionReasonCode.HighRiskApprovalRequired => PermissionReasonCodes.HighRiskApprovalRequired,
            PermissionReasonCode.ReadOnlyWriteDenied => PermissionReasonCodes.AccessDeniedByMode,
            PermissionReasonCode.ReadOnlyDeleteDenied => PermissionReasonCodes.AccessDeniedDeleteOperation,
            PermissionReasonCode.WriteModeDeleteDenied => PermissionReasonCodes.AccessDeniedDeleteOperation,
            PermissionReasonCode.WorkspaceNotResolved => PermissionReasonCodes.WorkspaceRootNotResolved,
            _ => PermissionReasonCodes.ToolDeniedByPolicy
        },
        Message = message,
        Command = command,
        WorkingDirectory = workingDirectory
    };

    public static SafeProcessResult Failed(string message, string command, string workingDirectory) => new()
    {
        Success = false,
        TimedOut = false,
        ExitCode = -1,
        ReasonCode = PermissionReasonCodes.ToolDeniedByPolicy,
        Message = message,
        Command = command,
        WorkingDirectory = workingDirectory
    };

    public static SafeProcessResult BlockedProcessExecution(string message, string command, string workingDirectory) => new()
    {
        Success = false,
        TimedOut = false,
        ExitCode = -1,
        ReasonCode = "BLOCKED_PROCESS_EXECUTION",
        Message = message,
        Command = command,
        WorkingDirectory = workingDirectory
    };

    public static SafeProcessResult InvalidCommand(string message, string command, string workingDirectory) => new()
    {
        Success = false,
        TimedOut = false,
        ExitCode = -1,
        ReasonCode = "INVALID_COMMAND",
        Message = message,
        Command = command,
        WorkingDirectory = workingDirectory
    };
}
