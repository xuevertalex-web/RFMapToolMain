using LocalCursorAgent.Indexing;

namespace LocalCursorAgent.Context
{
    public sealed class ProjectRetrievalPlan
    {
        public List<string> SelectedZones { get; init; } = new();
        public List<string> SelectedRoles { get; init; } = new();
        public string Reason { get; init; } = string.Empty;
        public double Confidence { get; init; }
        public bool FallbackUsed { get; init; }
    }

    public static class ProjectRetrievalPlanner
    {
        public static ProjectRetrievalPlan Plan(string task, ProjectMapSnapshot snapshot)
        {
            var text = (task ?? string.Empty).ToLowerInvariant();
            var zones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var reasons = new List<string>();

            Match(text, zones, roles, reasons, new[] { "ui", "webview", "extension", "status", "panel" }, "vscode-extension", "extension-ui", "ui_extension");
            Match(text, zones, roles, reasons, new[] { "safety", "permission", "approval", "guard" }, "Security", "test", "safety_guard");
            Match(text, zones, roles, reasons, new[] { "process", "command", "build" }, "Execution", "devtool", "process_build");
            Match(text, zones, roles, reasons, new[] { "context", "retrieval", "budget", "index" }, "Context", "indexing", "context_indexing");
            Match(text, zones, roles, reasons, new[] { "doctor", "smoke", "devtools", "script" }, "scripts/devtools", "devtool", "devtools");
            Match(text, zones, roles, reasons, new[] { "tests", "regression", "safetytests" }, "SafetyTests", "test", "tests");
            Match(text, zones, roles, reasons, new[] { "llm", "model", "provider" }, "LLM", "llm", "llm");
            Match(text, zones, roles, reasons, new[] { "memory", "session", "recent" }, "Memory", "memory", "memory");
            Match(text, zones, roles, reasons, new[] { "diagnostics", "payload", "tracing" }, "Diagnostics", "diagnostics", "diagnostics");

            if (zones.Count == 0 && roles.Count == 0)
            {
                return new ProjectRetrievalPlan
                {
                    SelectedZones = new List<string>(),
                    SelectedRoles = new List<string>(),
                    Reason = "no_keyword_match",
                    Confidence = 0.0,
                    FallbackUsed = true
                };
            }

            var selectedZones = zones
                .OrderBy(z => z, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var selectedRoles = roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();

            var confidence = Math.Min(0.95, 0.45 + (0.10 * reasons.Count));
            return new ProjectRetrievalPlan
            {
                SelectedZones = selectedZones,
                SelectedRoles = selectedRoles,
                Reason = string.Join("+", reasons.Distinct(StringComparer.OrdinalIgnoreCase)),
                Confidence = confidence,
                FallbackUsed = selectedZones.Count == 0 && selectedRoles.Count == 0
            };
        }

        private static void Match(string text, HashSet<string> zones, HashSet<string> roles, List<string> reasons, string[] keywords, string zone, string role, string reason)
        {
            if (!keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return;

            zones.Add(zone);
            roles.Add(role);
            reasons.Add(reason);
        }
    }
}
