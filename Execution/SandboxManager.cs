using System.Text.Json;

namespace LocalCursorAgent.Execution
{
    /// <summary>
    /// Stores persistent per-run backups only for paths the agent actually mutates.
    /// </summary>
    public sealed class SandboxManager
    {
        private const string SandboxDirectoryPrefix = "sandbox_";
        private const int MaxRetainedSandboxes = 2;
        private readonly string _sourcePath;
        private readonly string _sandboxRoot;
        private readonly HashSet<string> _capturedPaths = new(StringComparer.OrdinalIgnoreCase);
        private string? _sandboxPath;
        private string? _manifestPath;

        public string? SandboxPath => _sandboxPath;

        public SandboxManager(string sourcePath, string runtimeRoot)
        {
            _sourcePath = Path.GetFullPath(sourcePath ?? throw new ArgumentNullException(nameof(sourcePath)));
            _sandboxRoot = Path.Combine(Path.GetFullPath(runtimeRoot ?? throw new ArgumentNullException(nameof(runtimeRoot))), "sandboxes");
        }

        /// <summary>
        /// Initializes a new per-run backup container. No full workspace copy is created.
        /// </summary>
        public Task<bool> CreateSandbox()
        {
            if (!Directory.Exists(_sourcePath))
                return Task.FromResult(false);

            Directory.CreateDirectory(_sandboxRoot);
            _capturedPaths.Clear();

            var directoryName = $"{SandboxDirectoryPrefix}{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
            _sandboxPath = Path.Combine(_sandboxRoot, directoryName);
            Directory.CreateDirectory(_sandboxPath);

            _manifestPath = Path.Combine(_sandboxPath, "manifest.jsonl");
            File.WriteAllText(_manifestPath, string.Empty);
            AppendManifest(new BackupManifestEntry
            {
                EntryType = "session",
                OriginalPath = _sourcePath,
                RelativePath = ".",
                ItemKind = "workspace",
                Existed = true,
                BackupRelativePath = string.Empty,
                CapturedAtUtc = DateTime.UtcNow
            });

            PruneRetainedSandboxes();
            return Task.FromResult(true);
        }

        /// <summary>
        /// Captures the current on-disk state of a target path once per run before mutation.
        /// </summary>
        public async Task<BackupCaptureResult> CapturePathAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(_sandboxPath) || !Directory.Exists(_sandboxPath))
            {
                return BackupCaptureResult.Fail("Backup session is not initialized.");
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return BackupCaptureResult.Fail("Target path is empty.");
            }

            var resolvedPath = ResolvePath(path);
            if (!IsWithinSourceRoot(resolvedPath))
            {
                return BackupCaptureResult.Fail("Target path is outside the active workspace.");
            }

            if (!_capturedPaths.Add(resolvedPath))
                return BackupCaptureResult.Success(alreadyCaptured: true);

            var relativePath = Path.GetRelativePath(_sourcePath, resolvedPath);
            var backupRelativePath = Path.Combine("items", relativePath);
            var backupFullPath = Path.Combine(_sandboxPath, backupRelativePath);

            try
            {
                if (File.Exists(resolvedPath))
                {
                    var destinationDirectory = Path.GetDirectoryName(backupFullPath);
                    if (!string.IsNullOrWhiteSpace(destinationDirectory))
                        Directory.CreateDirectory(destinationDirectory);

                    await Task.Run(() => File.Copy(resolvedPath, backupFullPath, overwrite: false));
                    AppendManifest(new BackupManifestEntry
                    {
                        EntryType = "path",
                        OriginalPath = resolvedPath,
                        RelativePath = relativePath,
                        ItemKind = "file",
                        Existed = true,
                        BackupRelativePath = backupRelativePath,
                        CapturedAtUtc = DateTime.UtcNow
                    });
                    return BackupCaptureResult.Success();
                }

                if (Directory.Exists(resolvedPath))
                {
                    CopyDirectory(resolvedPath, backupFullPath);
                    AppendManifest(new BackupManifestEntry
                    {
                        EntryType = "path",
                        OriginalPath = resolvedPath,
                        RelativePath = relativePath,
                        ItemKind = "directory",
                        Existed = true,
                        BackupRelativePath = backupRelativePath,
                        CapturedAtUtc = DateTime.UtcNow
                    });
                    return BackupCaptureResult.Success();
                }

                AppendManifest(new BackupManifestEntry
                {
                    EntryType = "path",
                    OriginalPath = resolvedPath,
                    RelativePath = relativePath,
                    ItemKind = "missing",
                    Existed = false,
                    BackupRelativePath = string.Empty,
                    CapturedAtUtc = DateTime.UtcNow
                });
                return BackupCaptureResult.Success();
            }
            catch (Exception ex)
            {
                _capturedPaths.Remove(resolvedPath);
                return BackupCaptureResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Finalizes current run state while keeping retained backups on disk.
        /// </summary>
        public void CleanupSandbox()
        {
            _capturedPaths.Clear();
            _manifestPath = null;
            _sandboxPath = null;
        }

        private string ResolvePath(string path)
        {
            var fullPath = Path.IsPathFullyQualified(path)
                ? path
                : Path.Combine(_sourcePath, path);

            return Path.GetFullPath(fullPath);
        }

        private bool IsWithinSourceRoot(string path)
        {
            var normalizedRoot = _sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, overwrite: false);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                var destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(dir, destSubDir);
            }
        }

        private void PruneRetainedSandboxes()
        {
            var directories = new DirectoryInfo(_sandboxRoot)
                .GetDirectories($"{SandboxDirectoryPrefix}*")
                .OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var directory in directories.Skip(MaxRetainedSandboxes))
            {
                try
                {
                    directory.Delete(recursive: true);
                }
                catch
                {
                    // Best effort only. Active or locked backup sets must not break the current run.
                }
            }
        }

        private void AppendManifest(BackupManifestEntry entry)
        {
            if (string.IsNullOrWhiteSpace(_manifestPath))
                return;

            var json = JsonSerializer.Serialize(entry);
            File.AppendAllText(_manifestPath, json + Environment.NewLine);
        }

        private sealed class BackupManifestEntry
        {
            public string EntryType { get; init; } = string.Empty;
            public string OriginalPath { get; init; } = string.Empty;
            public string RelativePath { get; init; } = string.Empty;
            public string ItemKind { get; init; } = string.Empty;
            public bool Existed { get; init; }
            public string BackupRelativePath { get; init; } = string.Empty;
            public DateTime CapturedAtUtc { get; init; }
        }
    }

    public sealed class BackupCaptureResult
    {
        public bool Succeeded { get; init; }
        public bool AlreadyCaptured { get; init; }
        public string Message { get; init; } = string.Empty;

        public static BackupCaptureResult Success(bool alreadyCaptured = false) => new()
        {
            Succeeded = true,
            AlreadyCaptured = alreadyCaptured
        };

        public static BackupCaptureResult Fail(string message) => new()
        {
            Succeeded = false,
            Message = message ?? string.Empty
        };
    }
}
