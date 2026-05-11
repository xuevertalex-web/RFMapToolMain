using System.IO;

namespace LocalCursorAgent.Indexing
{
    public static class ProjectMapBuilder
    {
        public const string RulesVersion = "project-map-v1";

        public static ProjectMapSnapshot Build(string projectRoot, IEnumerable<string> relativePaths)
        {
            var normalized = (relativePaths ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var files = normalized
                .Select(path =>
                {
                    var zone = ClassifyZone(path);
                    var role = ClassifyRole(path, zone);
                    var isEntrypoint = IsEntrypoint(path);
                    var hints = BuildHints(zone, role, isEntrypoint);
                    return new FileMapEntry
                    {
                        Path = path,
                        Zone = zone,
                        Role = role,
                        IsEntrypoint = isEntrypoint,
                        Hints = hints
                    };
                })
                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ProjectMapSnapshot
            {
                GeneratedAtUtc = DateTime.UtcNow,
                FileCount = files.Count,
                Zones = files.Select(x => x.Zone).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                Files = files
            };
        }

        public static string ClassifyZone(string relativePath)
        {
            var path = NormalizePath(relativePath);
            if (StartsWith(path, "Core/")) return "Core";
            if (StartsWith(path, "Context/")) return "Context";
            if (StartsWith(path, "Indexing/")) return "Indexing";
            if (StartsWith(path, "Security/")) return "Security";
            if (StartsWith(path, "Execution/")) return "Execution";
            if (StartsWith(path, "Tools/")) return "Tools";
            if (StartsWith(path, "Diagnostics/")) return "Diagnostics";
            if (StartsWith(path, "Memory/")) return "Memory";
            if (StartsWith(path, "LLM/")) return "LLM";
            if (StartsWith(path, "SafetyTests/")) return "SafetyTests";
            if (StartsWith(path, "vscode-extension/")) return "vscode-extension";
            if (StartsWith(path, "scripts/devtools/")) return "scripts/devtools";
            if (StartsWith(path, "desktop-app/")) return "desktop-app";
            if (StartsWith(path, "docs/") || StartsWith(path, "config/") || StartsWith(path, "Configuration/")) return "docs/config";
            return "docs/config";
        }

        public static string ClassifyRole(string relativePath, string zone)
        {
            var path = NormalizePath(relativePath);
            var fileName = Path.GetFileName(path);
            var fileNameNoExt = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);

            if (IsEntrypoint(path)) return "entrypoint";
            if (zone.Equals("SafetyTests", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".test.js", StringComparison.OrdinalIgnoreCase) ||
                fileNameNoExt.Contains("test", StringComparison.OrdinalIgnoreCase))
                return "test";

            if (zone.Equals("scripts/devtools", StringComparison.OrdinalIgnoreCase)) return "devtool";
            if (zone.Equals("Diagnostics", StringComparison.OrdinalIgnoreCase)) return "diagnostics";
            if (zone.Equals("Memory", StringComparison.OrdinalIgnoreCase)) return "memory";
            if (zone.Equals("LLM", StringComparison.OrdinalIgnoreCase)) return "llm";
            if (zone.Equals("Context", StringComparison.OrdinalIgnoreCase)) return "context";
            if (zone.Equals("Indexing", StringComparison.OrdinalIgnoreCase)) return "indexing";
            if (zone.Equals("Security", StringComparison.OrdinalIgnoreCase)) return "security";
            if (zone.Equals("Execution", StringComparison.OrdinalIgnoreCase)) return "execution";
            if (zone.Equals("Tools", StringComparison.OrdinalIgnoreCase)) return "tool";
            if (zone.Equals("vscode-extension", StringComparison.OrdinalIgnoreCase))
            {
                if (fileName.Contains("webview", StringComparison.OrdinalIgnoreCase) ||
                    path.Contains("/resources/", StringComparison.OrdinalIgnoreCase) ||
                    path.Contains("/assets/", StringComparison.OrdinalIgnoreCase))
                    return "extension-ui";
                return "tool";
            }

            if (ext.Equals(".md", StringComparison.OrdinalIgnoreCase)) return "docs";
            if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".toml", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".config", StringComparison.OrdinalIgnoreCase))
                return "config";

            return "config";
        }

        public static bool IsEntrypoint(string relativePath)
        {
            var fileName = Path.GetFileName(NormalizePath(relativePath));
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            if (fileName.Equals("Program.cs", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.Equals("extension.js", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.Equals("main.js", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.Equals("app.js", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.Equals("index.js", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.Equals("package.json", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static List<string> BuildHints(string zone, string role, bool isEntrypoint)
        {
            var hints = new List<string> { $"zone:{zone}", $"role:{role}" };
            if (isEntrypoint)
                hints.Add("entrypoint:true");
            return hints;
        }

        private static string NormalizePath(string path) => (path ?? string.Empty).Replace('\\', '/').Trim();

        private static bool StartsWith(string text, string value) =>
            text.StartsWith(value, StringComparison.OrdinalIgnoreCase);
    }
}
