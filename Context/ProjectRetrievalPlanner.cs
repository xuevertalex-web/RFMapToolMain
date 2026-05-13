using LocalCursorAgent.Indexing;

namespace LocalCursorAgent.Context
{
    public sealed class ProjectRetrievalPlan
    {
        public List<string> SelectedZones { get; init; } = new();
        public List<string> SelectedRoles { get; init; } = new();
        public List<string> TopSignalFiles { get; init; } = new();
        public List<string> TopSignalReasons { get; init; } = new();
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

            // Augment with concept alias hints for natural-language queries
            var conceptMatches = QueryConceptAliasMapper.Detect(text);
            foreach (var match in conceptMatches)
            {
                bool added = false;
                foreach (var z in match.ZoneHints) added |= zones.Add(z);
                foreach (var r in match.RoleHints) added |= roles.Add(r);
                if (added)
                    reasons.Add("alias:" + match.Concept);
            }

            if (zones.Count == 0 && roles.Count == 0)
            {
                var scoredFallback = RetrievalSignalScorer.Score(text, snapshot).Take(6).ToList();
                return new ProjectRetrievalPlan
                {
                    SelectedZones = new List<string>(),
                    SelectedRoles = new List<string>(),
                    TopSignalFiles = scoredFallback.Select(x => x.Path).ToList(),
                    TopSignalReasons = scoredFallback.SelectMany(x => x.Reasons).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList(),
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
            var scored = RetrievalSignalScorer.Score(text, snapshot).Take(6).ToList();
            return new ProjectRetrievalPlan
            {
                SelectedZones = selectedZones,
                SelectedRoles = selectedRoles,
                TopSignalFiles = scored.Select(x => x.Path).ToList(),
                TopSignalReasons = scored.SelectMany(x => x.Reasons).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(6).ToList(),
                Reason = string.Join("+", reasons.Distinct(StringComparer.OrdinalIgnoreCase)) + (scored.Count > 0 ? $"+signals:{string.Join(",", scored.SelectMany(x => x.Reasons).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(4))}" : string.Empty),
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
