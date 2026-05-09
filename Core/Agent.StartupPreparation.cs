using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Indexing;
using LocalCursorAgent.Security;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class StartupPreparationResult
        {
            public required bool Success { get; init; }
            public TargetResolutionGateResult? TargetResolution { get; init; }
            public List<string>? GatedTargetFiles { get; init; }
            public string? FailureResult { get; init; }
        }

        private async Task<StartupPreparationResult> PrepareStartupAsync(string task, string? requestedNewFile)
        {
            var tracer = _contextBuilder.Tracer;
            tracer.LogActionEvent("IndexingStarted", "Agent", ExecutionTracer.ActionLogLevel.Info, "started");
            tracer.LogActionEvent("Indexing", "Agent", ExecutionTracer.ActionLogLevel.Info, "started");
            var indexResult = await _projectIndexer.IndexProject();
            tracer.UpdateRunIndexingStatus(indexResult.Success ? "completed" : "failed");
            tracer.LogActionEvent("IndexingCompleted", "Agent", indexResult.Success ? ExecutionTracer.ActionLogLevel.Info : ExecutionTracer.ActionLogLevel.Warning, indexResult.Success ? "completed" : "failed", indexResult.Success ? null : "INDEXING_FAILED", new Dictionary<string, object?>
            {
                { "files_processed", indexResult.FilesProcessed },
                { "error", indexResult.Error ?? string.Empty }
            });
            tracer.LogActionEvent("Indexing", "Agent", indexResult.Success ? ExecutionTracer.ActionLogLevel.Info : ExecutionTracer.ActionLogLevel.Warning, indexResult.Success ? "completed" : "failed", indexResult.Success ? null : "INDEXING_FAILED", new Dictionary<string, object?>
            {
                { "files_processed", indexResult.FilesProcessed },
                { "error", indexResult.Error ?? string.Empty }
            });

            if (indexResult.Success)
            {
                _memory.Add("indexing_complete", $"Indexed {indexResult.FilesProcessed} files");
            }

            var targetResolutionGate = new TargetResolutionGate(_projectIndexer, tracer);
            var targetResolution = await targetResolutionGate.ResolveAsync(task);
            if (targetResolution.IsFailed)
            {
                _memory.Add("target_resolution_gate", $"SKIPPED:{targetResolution.ReasonCode}:{targetResolution.Reason}");
            }

            var gatedTargetFiles = requestedNewFile is not null
                ? new List<string>()
                : targetResolution.IsResolved
                    ? targetResolution.SelectedFiles.ToList()
                    : null;

            ConfigureExecutionWorkspaceRoots();

            if (!await _sandboxManager.CreateSandbox())
            {
                var error = "Failed to create sandbox";
                _memory.Add("error", error, "SandboxCreationFailed");
                return new StartupPreparationResult
                {
                    Success = false,
                    FailureResult = FinalizeRunResult(false, error, "Sandbox creation failed", "SANDBOX_CREATION_FAILED", Array.Empty<string>(), Array.Empty<ChangedHint>(), Array.Empty<ChangedRange>(), Array.Empty<ChangedKind>(), false)
                };
            }

            return new StartupPreparationResult
            {
                Success = true,
                TargetResolution = targetResolution,
                GatedTargetFiles = gatedTargetFiles
            };
        }

        private void ConfigureExecutionWorkspaceRoots()
        {
            if (_sessionContext is null)
                return;

            _sessionContext.ExecutionWorkspaceRoot = _sessionContext.ActiveWorkspaceRoot;
            _sessionContext.WorktreeRoot = _sessionContext.ActiveWorkspaceRoot;
            _sessionContext.ExecutionWorkspaceKind = "active-workspace";
            _sessionContext.ActiveWorkspaceUsed = true;

            if (!AgentExecutionProfile.RequiresIsolatedWorktree(_sessionContext))
                return;

            var worktreeRoot = Path.Combine(_sessionContext.RuntimeRoot, "worktrees", _sessionContext.SessionId);
            Directory.CreateDirectory(Path.GetDirectoryName(worktreeRoot)!);
            if (Directory.Exists(worktreeRoot))
                Directory.Delete(worktreeRoot, recursive: true);
            CopyDirectory(_sessionContext.ActiveWorkspaceRoot, worktreeRoot);

            _sessionContext.ExecutionWorkspaceRoot = worktreeRoot;
            _sessionContext.WorktreeRoot = worktreeRoot;
            _sessionContext.ExecutionWorkspaceKind = "worktree";
            _sessionContext.ActiveWorkspaceUsed = false;
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var target = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, target, overwrite: true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var targetDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, targetDir);
            }
        }
    }
}
