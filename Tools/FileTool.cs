using LocalCursorAgent.Security;
using LocalCursorAgent.Diagnostics;
using System.Text.RegularExpressions;
using LocalCursorAgent.Execution;

namespace LocalCursorAgent.Tools
{
    /// <summary>
    /// Tool for reading and writing files.
    /// </summary>
    public class FileTool : ITool
    {
        public string Name => "file";
        public string Description => "Read/write/delete/rename/move files. Format: 'read:<path>', 'write:<path>:<content>', 'delete:<path>', 'rename:<source>:<destination>', 'move:<source>:<destination>'";

        private readonly AgentSessionContext _session;
        private readonly PermissionGuard _permissionGuard;
        private readonly PatchSafetyGate _patchSafetyGate;
        private readonly DestructiveOperationSafetyGate _destructiveOperationSafetyGate;
        private readonly SandboxManager _sandboxManager;
        private readonly TextFileService _textFileService;
        private readonly ExecutionTracer? _tracer;

        public FileTool(AgentSessionContext session, PermissionGuard permissionGuard, PatchSafetyGate patchSafetyGate, DestructiveOperationSafetyGate destructiveOperationSafetyGate, SandboxManager sandboxManager, ExecutionTracer? tracer = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _permissionGuard = permissionGuard ?? throw new ArgumentNullException(nameof(permissionGuard));
            _patchSafetyGate = patchSafetyGate ?? throw new ArgumentNullException(nameof(patchSafetyGate));
            _destructiveOperationSafetyGate = destructiveOperationSafetyGate ?? throw new ArgumentNullException(nameof(destructiveOperationSafetyGate));
            _sandboxManager = sandboxManager ?? throw new ArgumentNullException(nameof(sandboxManager));
            _textFileService = new TextFileService();
            _tracer = tracer;
        }

        public async Task<string> Execute(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Error: No command provided";

            if (input.StartsWith("read:", StringComparison.OrdinalIgnoreCase))
                return await ReadFile(input.Substring(5).Trim());

            if (input.StartsWith("write:", StringComparison.OrdinalIgnoreCase))
                return await WriteFileCommand(input.Substring(6));

            if (input.StartsWith("delete:", StringComparison.OrdinalIgnoreCase))
                return await DeletePath(input.Substring(7).Trim());

            if (input.StartsWith("rename:", StringComparison.OrdinalIgnoreCase))
                return await RenameOrMove(input.Substring(7), isMove: false);

            if (input.StartsWith("move:", StringComparison.OrdinalIgnoreCase))
                return await RenameOrMove(input.Substring(5), isMove: true);

            return "Error: Unknown command. Use 'read', 'write', 'delete', 'rename', or 'move'";
        }

        private async Task<string> ReadFile(string path)
        {
            var action = new ToolAction
            {
                Kind = ToolActionKind.ReadFile,
                TargetPath = ResolvePath(path)
            };

            var decision = _permissionGuard.Evaluate(_session, action);
            if (!decision.Allowed)
                return FormatDenied(decision);

            if (!File.Exists(action.TargetPath))
                return $"Error: File not found - {path}";

            var snapshot = await _textFileService.ReadAsync(action.TargetPath!);
            _tracer?.LogActionEvent("FileAction", "FileTool", ExecutionTracer.ActionLogLevel.Info, "completed", metadata: new Dictionary<string, object?>
            {
                { "operation", "read" },
                { "requested_path", path },
                { "normalized_path", action.TargetPath! },
                { "file_state_changed", false }
            });
            return snapshot.TextContent;
        }

        private async Task<string> WriteFileCommand(string payload)
        {
            var separator = FindCommandSeparator(payload);
            if (separator < 0)
                return "Error: Invalid write format. Use 'write:<path>:<content>'";

            var filePath = payload[..separator].Trim();
            var content = payload[(separator + 1)..];
            return await WriteFile(filePath, content);
        }

        private async Task<string> WriteFile(string path, string content)
        {
            content = DecodeEscapedNewlines(content);
            var fullOverwrite = true;

            var action = new ToolAction
            {
                Kind = ToolActionKind.WriteFile,
                TargetPath = ResolvePath(path),
                Payload = content
            };

            var decision = _permissionGuard.Evaluate(_session, action);
            if (!decision.Allowed)
                return FormatDenied(decision);

            var resolvedPath = action.TargetPath!;
            var backupCapture = await _sandboxManager.CapturePathAsync(resolvedPath);
            if (!backupCapture.Succeeded)
                return $"DENIED [{PermissionReasonCodes.BackupCaptureFailed}]: {backupCapture.Message}";

            var directory = Path.GetDirectoryName(resolvedPath);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            _tracer?.LogActionEvent("FileAction", "FileTool", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
            {
                { "operation", "write" },
                { "requested_path", path },
                { "normalized_path", resolvedPath },
                { "content_length", content.Length }
            });

            var sanitized = SanitizeWriteContent(resolvedPath, content);
            if (!sanitized.IsValid)
                return $"DENIED [INVALID_CSHARP_OUTPUT]: {sanitized.Reason}";

            content = sanitized.Content;
            TextFileSnapshot? existing = File.Exists(resolvedPath) ? _textFileService.Read(resolvedPath) : null;
            var originalBytes = File.Exists(resolvedPath) ? await File.ReadAllBytesAsync(resolvedPath) : null;
            PatchApplyResult applyResult;
            if (fullOverwrite)
            {
                try
                {
                    await _textFileService.WriteAsync(resolvedPath, content, existing);
                    applyResult = new PatchApplyResult
                    {
                        ApplySucceeded = true,
                        ReasonCode = PermissionReasonCodes.Allowed,
                        Message = "Full overwrite applied successfully.",
                        TargetPath = resolvedPath
                    };
                }
                catch (Exception ex)
                {
                    if (originalBytes == null)
                    {
                        if (File.Exists(resolvedPath))
                            File.Delete(resolvedPath);
                    }
                    else
                    {
                        await File.WriteAllBytesAsync(resolvedPath, originalBytes);
                    }

                    applyResult = new PatchApplyResult
                    {
                        ApplyFailed = true,
                        ReasonCode = PermissionReasonCodes.PatchApplyFailed,
                        Message = ex.Message,
                        TargetPath = resolvedPath
                    };
                }
            }
            else
            {
                var preview = _patchSafetyGate.Preview(resolvedPath, resolvedPath, content, ExtractAnchorHint(content));
                if (preview.PreviewRejected)
                    return $"DENIED [{preview.ReasonCode}]: {preview.Message}";

                applyResult = await _patchSafetyGate.ApplyAsync(
                    preview,
                    async () => await _textFileService.WriteAsync(resolvedPath, content, existing),
                    rollbackAction: originalBytes == null
                        ? async () =>
                        {
                            if (File.Exists(resolvedPath))
                                File.Delete(resolvedPath);
                            await Task.CompletedTask;
                        }
                        : async () =>
                        {
                            await File.WriteAllBytesAsync(resolvedPath, originalBytes);
                        });
            }

            if (applyResult.ApplySucceeded)
            {
                _tracer?.MarkChangedFile(resolvedPath);
                _tracer?.LogActionEvent("FileAction", "FileTool", ExecutionTracer.ActionLogLevel.Info, "completed", applyResult.ReasonCode, new Dictionary<string, object?>
                {
                    { "operation", "write" },
                    { "requested_path", path },
                    { "normalized_path", resolvedPath },
                    { "preview_outcome", fullOverwrite ? "full_overwrite" : "patch_preview" },
                    { "apply_outcome", "applied" },
                    { "file_state_changed", true }
                });
                return $"Successfully wrote to {path}";
            }

            _tracer?.LogActionEvent("FileAction", "FileTool", ExecutionTracer.ActionLogLevel.Warning, "failed", applyResult.ReasonCode, new Dictionary<string, object?>
            {
                { "operation", "write" },
                { "requested_path", path },
                { "normalized_path", resolvedPath },
                { "apply_outcome", "denied" },
                { "file_state_changed", false }
            });

            return $"DENIED [{applyResult.ReasonCode}]: {applyResult.Message}";
        }

        private async Task<string> DeletePath(string path)
        {
            var resolvedPath = ResolvePath(path);
            var backupCapture = await _sandboxManager.CapturePathAsync(resolvedPath);
            if (!backupCapture.Succeeded)
                return $"DENIED [{PermissionReasonCodes.BackupCaptureFailed}]: {backupCapture.Message}";

            var result = await _destructiveOperationSafetyGate.DeleteAsync(path);
            if (result.DestructiveApplySucceeded)
                _tracer?.MarkChangedFile(resolvedPath);
            return result.DestructiveApplySucceeded
                ? (File.Exists(resolvedPath) ? $"Successfully deleted file {path}" : $"Successfully deleted directory {path}")
                : $"DENIED [{result.ReasonCode}]: {result.Message}";
        }

        private async Task<string> RenameOrMove(string payload, bool isMove)
        {
            var separator = FindCommandSeparator(payload);
            if (separator < 0)
                return isMove
                    ? "Error: Invalid move format. Use 'move:<source>:<destination>'"
                    : "Error: Invalid rename format. Use 'rename:<source>:<destination>'";

            var source = payload[..separator].Trim();
            var destination = payload[(separator + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
                return "Error: Source and destination paths are required";

            var action = new ToolAction
            {
                Kind = isMove ? ToolActionKind.MoveFile : ToolActionKind.RenameFile,
                SourcePath = ResolvePath(source),
                DestinationPath = ResolvePath(destination)
            };

            var decision = _permissionGuard.Evaluate(_session, action);
            if (!decision.Allowed)
                return FormatDenied(decision);

            var resolvedSource = action.SourcePath!;
            var resolvedDestination = action.DestinationPath!;
            var backupCapture = await _sandboxManager.CapturePathAsync(resolvedSource);
            if (!backupCapture.Succeeded)
                return $"DENIED [{PermissionReasonCodes.BackupCaptureFailed}]: {backupCapture.Message}";

            var result = await _destructiveOperationSafetyGate.RenameAsync(source, destination, isMove);
            if (result.DestructiveApplySucceeded)
                _tracer?.MarkChangedFile(resolvedDestination);
            return result.DestructiveApplySucceeded
                ? (isMove
                    ? $"Successfully moved {(File.Exists(resolvedDestination) ? "file" : "directory")} {source} to {destination}"
                    : $"Successfully renamed {(File.Exists(resolvedDestination) ? "file" : "directory")} {source} to {destination}")
                : $"DENIED [{result.ReasonCode}]: {result.Message}";
        }

        private string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return _session.ActiveWorkspaceRoot;

            var fullPath = Path.IsPathFullyQualified(path)
                ? path
                : Path.Combine(_session.ActiveWorkspaceRoot, path);

            return Path.GetFullPath(fullPath);
        }

        private static string FormatDenied(PermissionDecision decision) =>
            $"DENIED [{decision.ReasonCodeString}]: {decision.Message}";

        private static string? ExtractAnchorHint(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            var firstNonEmptyLine = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            return firstNonEmptyLine;
        }

        private static int FindCommandSeparator(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return -1;

            if (payload.Length >= 3 && payload[1] == ':' && (payload[2] == '\\' || payload[2] == '/'))
                return payload.IndexOf(':', 3);

            return payload.IndexOf(':');
        }

        private static string DecodeEscapedNewlines(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return content;

            if (!content.Contains("\\n", StringComparison.Ordinal) && !content.Contains("\\r", StringComparison.Ordinal))
                return content;

            return content
                .Replace("\\r\\n", "\r\n", StringComparison.Ordinal)
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\r", "\r", StringComparison.Ordinal);
        }

        private static SanitizedWriteContent SanitizeWriteContent(string path, string content)
        {
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                return SanitizedWriteContent.Valid(content);

            var normalized = content.Replace("@using ", "using ", StringComparison.Ordinal);
            normalized = normalized.Replace("@namespace ", "namespace ", StringComparison.Ordinal);
            normalized = Regex.Replace(normalized, @"(^|\s)@(?=(?:public|internal|private|protected|sealed|static|abstract|partial|class|interface|struct|record|enum)\b)", "$1", RegexOptions.Multiline);

            var trimmed = normalized.TrimStart();
            if (trimmed.StartsWith("@using ", StringComparison.Ordinal) ||
                trimmed.StartsWith("@namespace ", StringComparison.Ordinal) ||
                trimmed.StartsWith("@class ", StringComparison.Ordinal))
            {
                return SanitizedWriteContent.Invalid("The generated C# file starts with malformed directive tokens.");
            }

            var hasMalformedKeyword = Regex.IsMatch(normalized, @"^\s*@(using|namespace|class|public|internal|private|protected|sealed|static|abstract|partial)\b", RegexOptions.Multiline);
            if (hasMalformedKeyword)
                return SanitizedWriteContent.Invalid("The generated C# file contains malformed declaration keywords.");

            var hasTopLevelJunkBeforeDeclaration = Regex.IsMatch(
                trimmed,
                @"^(?!using\b|namespace\b|public\b|internal\b|file\b|sealed\b|static\b|abstract\b|partial\b|\[)",
                RegexOptions.Singleline);

            if (hasTopLevelJunkBeforeDeclaration)
                return SanitizedWriteContent.Invalid("The generated C# file starts with syntactically suspicious content.");

            return SanitizedWriteContent.Valid(normalized);
        }

        private sealed record SanitizedWriteContent(string Content, bool IsValid, string Reason)
        {
            public static SanitizedWriteContent Valid(string content) => new(content, true, string.Empty);
            public static SanitizedWriteContent Invalid(string reason) => new(string.Empty, false, reason);
        }
    }
}
