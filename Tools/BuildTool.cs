using LocalCursorAgent.Execution;
using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Security;

namespace LocalCursorAgent.Tools
{
    /// <summary>
    /// Tool for executing build commands (dotnet build).
    /// </summary>
    public class BuildTool : ITool
    {
        public string Name => "build";
        public string Description => "Execute 'dotnet build' in the specified project directory";

        private readonly AgentSessionContext _session;
        private readonly PermissionGuard _permissionGuard;
        private readonly SafeProcessRunner _processRunner;

        public BuildTool(AgentSessionContext session, PermissionGuard permissionGuard, ExecutionTracer? tracer = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _permissionGuard = permissionGuard ?? throw new ArgumentNullException(nameof(permissionGuard));
            _processRunner = new SafeProcessRunner(_session, _permissionGuard, tracer);
        }

        public async Task<string> Execute(string input)
        {
            var workingDirectory = ResolveWorkingDirectory(input);
            var action = new ToolAction
            {
                Kind = ToolActionKind.Build,
                WorkingDirectory = workingDirectory
            };

            var decision = _permissionGuard.Evaluate(_session, action);
            if (!decision.Allowed)
                return $"DENIED [{decision.ReasonCodeString}]: {decision.Message}";

            if (!Directory.Exists(workingDirectory))
                return $"Error: Directory not found - {workingDirectory}";

            var result = await _processRunner.RunAsync(new SafeProcessRequest
            {
                Kind = ToolActionKind.Build,
                Command = "dotnet",
                Args = SafeProcessRunner.GetDefaultBuildArgs(),
                WorkingDirectory = workingDirectory
            });

            return FormatResult(result);
        }

        private string ResolveWorkingDirectory(string input)
        {
            var candidate = string.IsNullOrWhiteSpace(input) ? _session.ActiveWorkspaceRoot : input.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
                return _session.ActiveWorkspaceRoot;

            if (candidate.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                candidate.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                candidate = Path.GetDirectoryName(candidate) ?? candidate;
            }

            var fullPath = Path.IsPathFullyQualified(candidate)
                ? candidate
                : Path.Combine(_session.ActiveWorkspaceRoot, candidate);

            return Path.GetFullPath(fullPath);
        }

        private static string FormatResult(SafeProcessResult result)
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(result.StdOut))
                lines.Add(result.StdOut.TrimEnd());
            if (!string.IsNullOrWhiteSpace(result.StdErr))
                lines.Add("STDERR:" + Environment.NewLine + result.StdErr.TrimEnd());

            if (lines.Count > 0)
                return string.Join(Environment.NewLine, lines);

            if (result.TimedOut)
                return "Error: Build timed out";

            if (!result.Success)
                return $"Error: {result.Message}";

            return "Build completed";
        }
    }
}
