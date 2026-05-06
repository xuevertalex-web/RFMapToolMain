using System.Diagnostics;
using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Security;
using LocalCursorAgent.Tools;

namespace LocalCursorAgent.Execution
{
    /// <summary>
    /// Verifies build success and extracts compilation errors.
    /// </summary>
    public class BuildVerifier
    {
        private readonly SafeProcessRunner _processRunner;
        private readonly ExecutionTracer? _tracer;

        public BuildVerifier(SafeProcessRunner processRunner, ExecutionTracer? tracer = null)
        {
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
            _tracer = tracer;
        }

        /// <summary>
        /// Result of a build verification.
        /// </summary>
        public class BuildResult
        {
            public bool Success { get; set; }
            public List<string> Errors { get; set; } = new();
            public List<string> Warnings { get; set; } = new();
            public string FullOutput { get; set; } = string.Empty;
            public string ReasonCode { get; set; } = string.Empty;
            public bool TimedOut { get; set; }
            public int ExitCode { get; set; }
        }

        /// <summary>
        /// Run dotnet build and verify the result.
        /// </summary>
        public async Task<BuildResult> VerifyBuild(string projectPath)
        {
            var result = new BuildResult();
            var startedAt = DateTime.UtcNow;
            _tracer?.LogActionEvent("BuildVerification", "BuildVerifier", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
            {
                { "project_path", projectPath }
            });

            if (!Directory.Exists(projectPath))
            {
                result.Success = false;
                result.ReasonCode = PermissionReasonCodes.InvalidWorkingDirectory;
                result.Errors.Add($"Project path not found: {projectPath}");
                _tracer?.LogActionEvent("BuildVerification", "BuildVerifier", ExecutionTracer.ActionLogLevel.Warning, "failed", PermissionReasonCodes.InvalidWorkingDirectory, new Dictionary<string, object?>
                {
                    { "project_path", projectPath }
                }, durationMs: (long)(DateTime.UtcNow - startedAt).TotalMilliseconds);
                return result;
            }

            SafeProcessResult safeResult;
            if (Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(Path.GetDirectoryName(projectPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                safeResult = await RunDotnetBuildDirectly(projectPath);
            }
            else
            {
                safeResult = await _processRunner.RunAsync(new SafeProcessRequest
                {
                    Kind = ToolActionKind.Build,
                    Command = "dotnet",
                    Args = SafeProcessRunner.GetDefaultBuildArgs(),
                    WorkingDirectory = projectPath
                });
            }

            _tracer?.LogActionEvent("Build", "BuildVerifier", safeResult.Success ? ExecutionTracer.ActionLogLevel.Info : ExecutionTracer.ActionLogLevel.Warning, safeResult.Success ? "completed" : (safeResult.TimedOut ? "timed_out" : "failed"), safeResult.ReasonCode, new Dictionary<string, object?>
            {
                { "project_path", projectPath },
                { "command", safeResult.Command },
                { "arguments", safeResult.Arguments },
                { "working_directory", safeResult.WorkingDirectory },
                { "exit_code", safeResult.ExitCode },
                { "timed_out", safeResult.TimedOut },
                { "stdout_size", safeResult.StdOut.Length },
                { "stderr_size", safeResult.StdErr.Length }
            });

            result.FullOutput = string.Join("\n", new[] { safeResult.StdOut, safeResult.StdErr }.Where(s => !string.IsNullOrWhiteSpace(s)));
            result.Success = safeResult.Success && !safeResult.TimedOut;
            result.ReasonCode = safeResult.ReasonCode ?? string.Empty;
            result.TimedOut = safeResult.TimedOut;
            result.ExitCode = safeResult.ExitCode;
            if (!safeResult.Success && !string.IsNullOrWhiteSpace(safeResult.Message))
            {
                result.Errors.Add(safeResult.Message);
            }

            var lines = result.FullOutput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (line.Contains("error CS", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(line.Trim());
                }
                else if (line.Contains("warning CS", StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add(line.Trim());
                }
            }

            _tracer?.LogActionEvent("BuildVerification", "BuildVerifier", result.Success ? ExecutionTracer.ActionLogLevel.Info : ExecutionTracer.ActionLogLevel.Warning, result.Success ? "completed" : "failed", result.Success ? PermissionReasonCodes.Allowed : "BUILD_FAILED", new Dictionary<string, object?>
            {
                { "project_path", projectPath },
                { "error_count", result.Errors.Count },
                { "warning_count", result.Warnings.Count }
            }, durationMs: (long)(DateTime.UtcNow - startedAt).TotalMilliseconds);
            return result;
        }

        private static async Task<SafeProcessResult> RunDotnetBuildDirectly(string workingDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = string.Join(" ", SafeProcessRunner.GetDefaultBuildArgs()),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            try
            {
                if (!process.Start())
                    return SafeProcessResult.Failed("Failed to start process", "dotnet", workingDirectory);
            }
            catch (Exception ex)
            {
                return SafeProcessResult.Failed(ex.Message, "dotnet", workingDirectory);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            await Task.WhenAll(stdoutTask, stderrTask);

            return new SafeProcessResult
            {
                Success = process.ExitCode == 0,
                TimedOut = false,
                ExitCode = process.ExitCode,
                StdOut = await stdoutTask,
                StdErr = await stderrTask,
                Command = "dotnet",
                Arguments = "build",
                WorkingDirectory = workingDirectory,
                ReasonCode = process.ExitCode == 0 ? PermissionReasonCodes.Allowed : "BUILD_FAILED"
            };
        }
    }
}
