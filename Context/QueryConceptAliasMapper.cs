namespace LocalCursorAgent.Context
{
    public sealed class QueryConceptMatch
    {
        public string Concept { get; init; } = string.Empty;
        public List<string> MatchedAliases { get; init; } = new();
        public List<string> ZoneHints { get; init; } = new();
        public List<string> RoleHints { get; init; } = new();
        public List<string> FileNameHints { get; init; } = new();
    }

    public static class QueryConceptAliasMapper
    {
        private sealed record ConceptRule(string Concept, string[] Aliases, string[] ZoneHints, string[] RoleHints, string[] FileNameHints);

        private static readonly ConceptRule[] Rules =
        {
            new("workspace_bootstrap", new[] { "workspace", "bootstrap", "init", "иниц", "разбор", "targetworkspacepath" }, new[] { "Core", "Security" }, new[] { "context", "security" }, new[] { "workspaceResolver", "StartupPreparation", "targetWorkspacePath" }),
            new("intent_routing", new[] { "intent", "routing", "chat", "clarify", "execute", "маршрутиз", "уточн" }, new[] { "Core" }, new[] { "context" }, new[] { "IntentDecisionEngine", "TaskIntentScorer", "RunLoopBranches" }),
            new("payload_result_status", new[] { "payload", "result", "status", "статус", "ответ", "summary" }, new[] { "Core", "vscode-extension" }, new[] { "diagnostics", "extension-ui" }, new[] { "RunResultPayloadBuilder", "PayloadTypes", "webviewClientResultHandlers" }),
            new("ui_webview_panel", new[] { "ui", "webview", "panel", "status", "интерфейс", "панел" }, new[] { "vscode-extension" }, new[] { "extension-ui" }, new[] { "webview", "panelRunController", "resultHandlers" }),
            new("guard_approval_security", new[] { "guard", "approval", "permission", "security", "безопас", "доступ" }, new[] { "Security", "Core" }, new[] { "security", "test" }, new[] { "PermissionGuard", "GuardedTool", "workspaceResolver" }),
            new("process_command_build", new[] { "process", "command", "shell", "build", "процесс", "сборк", "команд" }, new[] { "Execution", "Core" }, new[] { "execution", "devtool" }, new[] { "SafeProcessRunner", "BuildVerifier", "Execution" }),
            new("context_retrieval_budget_map", new[] { "context", "retrieval", "budget", "map", "контекст", "поиск", "бюджет" }, new[] { "Context", "Indexing" }, new[] { "context", "indexing" }, new[] { "ContextBuilder", "ProjectRetrievalPlanner", "ProjectMap" }),
            new("doctor_smoke_devtools_update", new[] { "doctor", "smoke", "devtools", "update", "диагност", "смок" }, new[] { "scripts/devtools" }, new[] { "devtool" }, new[] { "Doctor", "SmokeGate", "Update-VSCodeExtension" }),
            new("memory_session_recent", new[] { "memory", "session", "recent", "памят", "сесс", "недавн" }, new[] { "Memory", "Diagnostics" }, new[] { "memory", "diagnostics" }, new[] { "Memory", "session", "recent" })
        };

        public static List<QueryConceptMatch> Detect(string task)
        {
            var text = (task ?? string.Empty).ToLowerInvariant();
            return Rules
                .Select(rule => new QueryConceptMatch
                {
                    Concept = rule.Concept,
                    MatchedAliases = rule.Aliases.Where(a => text.Contains(a, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList(),
                    ZoneHints = rule.ZoneHints.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    RoleHints = rule.RoleHints.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    FileNameHints = rule.FileNameHints.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
                })
                .Where(x => x.MatchedAliases.Count > 0)
                .OrderBy(x => x.Concept, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
