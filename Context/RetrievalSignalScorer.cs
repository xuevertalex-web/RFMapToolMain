using LocalCursorAgent.Indexing;

namespace LocalCursorAgent.Context
{
    public sealed class RetrievalSignalScore
    {
        public string Path { get; init; } = string.Empty;
        public double Score { get; init; }
        public List<string> Reasons { get; init; } = new();
    }

    public static class RetrievalSignalScorer
    {
        public static List<RetrievalSignalScore> Score(string task, ProjectMapSnapshot snapshot)
        {
            var text = (task ?? string.Empty).ToLowerInvariant();
            var aliasMatches = QueryConceptAliasMapper.Detect(text);
            var aliasTokens = aliasMatches.SelectMany(x => x.MatchedAliases).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var aliasFileHints = aliasMatches.SelectMany(x => x.FileNameHints).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var scored = new List<RetrievalSignalScore>();
            foreach (var file in snapshot.Files.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
            {
                var reasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var score = 0.0;
                var path = file.Path ?? string.Empty;
                var fileName = Path.GetFileName(path);
                var fileStem = Path.GetFileNameWithoutExtension(path);
                var normalizedPath = path.Replace('\\', '/');

                if (ContainsAny(text, fileName, fileStem))
                {
                    score += 1.0;
                    reasons.Add("path-match");
                }

                if (aliasTokens.Any(a => ContainsToken(path, a) || ContainsToken(fileName, a) || ContainsToken(fileStem, a)))
                {
                    score += 0.9;
                    reasons.Add("concept-alias");
                }

                if (aliasFileHints.Any(h => ContainsToken(path, h) || ContainsToken(fileName, h) || ContainsToken(fileStem, h)))
                {
                    score += 1.2;
                    reasons.Add("symbol-match");
                }

                if (IsSecuritySurfacePath(normalizedPath))
                {
                    score += 0.8;
                    reasons.Add("security-surface");
                }

                if (IsZoneRoleHit(text, file.Zone, file.Role))
                {
                    score += 0.6;
                    reasons.Add("zone-role");
                }

                score += GetTaskSpecificBoost(text, normalizedPath, fileName, fileStem, reasons);

                if (score <= 0)
                    continue;

                scored.Add(new RetrievalSignalScore
                {
                    Path = path,
                    Score = score,
                    Reasons = reasons.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
                });
            }

            return scored
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsSecuritySurfacePath(string path)
        {
            return path.StartsWith("Security/", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Tools/", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Execution/", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Core/Agent", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("vscode-extension/workspace", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("/Run", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("scripts/devtools/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsZoneRoleHit(string text, string zone, string role)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            return ContainsToken(text, zone) || ContainsToken(text, role);
        }

        private static double GetTaskSpecificBoost(string text, string path, string fileName, string fileStem, HashSet<string> reasons)
        {
            var boost = 0.0;
            var mixedWorkspaceApproval = ContainsAnyToken(text, "workspace", "boundary", "guard", "\u043e\u0431\u043e\u0439\u0442\u0438", "\u0440\u0430\u0431\u043e\u0447\u0430\u044f \u043e\u0431\u043b\u0430\u0441\u0442\u044c")
                && ContainsAnyToken(text, "approval", "token", "permission", "security", "\u0440\u0430\u0437\u0440\u0435\u0448\u0435\u043d\u0438\u0435");
            if (ContainsAnyToken(text, "approval", "token", "permission", "security", "destructive", "delete", "overwrite", "rename"))
            {
                if (path.StartsWith("Security/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Tools/", StringComparison.OrdinalIgnoreCase) || fileName.Equals("FileTool.cs", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Core/Agent.Tooling", StringComparison.OrdinalIgnoreCase) || path.Equals("SafetyTests/Program.cs", StringComparison.OrdinalIgnoreCase))
                {
                    boost += fileName.Equals("FileTool.cs", StringComparison.OrdinalIgnoreCase) ? 2.2 : 1.5;
                    reasons.Add("task-approval-security");
                }
            }
            if (ContainsAnyToken(text, "workspace", "boundary", "guard", "обойти", "рабочая область", "разрешение"))
            {
                if (ContainsToken(fileName, "workspaceResolver") || ContainsToken(fileName, "workspaceTaskClassifier") || ContainsToken(fileName, "panelRunController") || ContainsToken(fileName, "commandHandlers") || path.Contains("workspace", StringComparison.OrdinalIgnoreCase) || ContainsToken(fileName, "WorkspacePolicy"))
                {
                    var exactWorkspaceSurface = fileName.Equals("workspaceResolver.js", StringComparison.OrdinalIgnoreCase) ||
                                                fileName.Equals("workspaceTaskClassifier.js", StringComparison.OrdinalIgnoreCase) ||
                                                fileName.Equals("panelRunController.js", StringComparison.OrdinalIgnoreCase) ||
                                                fileName.Equals("commandHandlers.js", StringComparison.OrdinalIgnoreCase);
                    var mixedBonus = mixedWorkspaceApproval && exactWorkspaceSurface ? 1.2 : 0.0;
                    boost += (exactWorkspaceSurface ? 2.6 : 1.8) + mixedBonus;
                    reasons.Add("task-workspace-boundary");
                    if (mixedBonus > 0)
                        reasons.Add("task-mixed-workspace-approval");
                }
            }
            if (ContainsAnyToken(text, "command", "process", "shell"))
            {
                if (fileName.Equals("SafeProcessRunner.cs", StringComparison.OrdinalIgnoreCase) || fileName.Equals("CommandRiskPolicy.cs", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Tools/", StringComparison.OrdinalIgnoreCase) || path.Equals("SafetyTests/Program.cs", StringComparison.OrdinalIgnoreCase))
                {
                    boost += fileName.Equals("SafeProcessRunner.cs", StringComparison.OrdinalIgnoreCase) ? 2.4 : 1.6;
                    reasons.Add("task-command-process");
                }
            }
            if (ContainsAnyToken(text, "retrieval", "context", "deep analysis"))
            {
                if (path.Equals("Context/ContextBuilder.cs", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("Context/RetrievalSignalScorer.cs", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("Context/ProjectRetrievalPlanner.cs", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("Core/Agent.ContextPreparation.cs", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("Core/AnalysisContextFormatter.cs", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("Core/AnalysisPromptBuilder.cs", StringComparison.OrdinalIgnoreCase))
                {
                    boost += 1.9; reasons.Add("task-retrieval-context");
                }
            }
            if (ContainsAnyToken(text, "payload", "result", "status"))
            {
                if (path.StartsWith("Core/Agent.RunResult", StringComparison.OrdinalIgnoreCase))
                {
                    boost += 1.1; reasons.Add("task-payload-result");
                }
            }
            if (ContainsAnyToken(text, "install", "vsix", "update", "workflow", "stale", "package", "расширение", "установка", "пакет"))
            {
                if (path.Equals("scripts/devtools/Update-VSCodeExtension.cmd", StringComparison.OrdinalIgnoreCase) || path.Equals("vscode-extension/package.json", StringComparison.OrdinalIgnoreCase) || path.Contains("package", StringComparison.OrdinalIgnoreCase) || path.Contains("update", StringComparison.OrdinalIgnoreCase))
                {
                    boost += (path.Equals("scripts/devtools/Update-VSCodeExtension.cmd", StringComparison.OrdinalIgnoreCase) || path.Equals("vscode-extension/package.json", StringComparison.OrdinalIgnoreCase)) ? 2.4 : 1.3;
                    reasons.Add("task-install-update");
                }
            }
            if (ContainsAnyToken(text, "snapshot", "source archive"))
            {
                if (path.Equals("scripts/Create-SourceSnapshot.ps1", StringComparison.OrdinalIgnoreCase) || path.Equals("scripts/devtools/Create-SourceSnapshot.cmd", StringComparison.OrdinalIgnoreCase))
                {
                    boost += 1.4; reasons.Add("task-snapshot");
                }
            }
            if (ContainsAnyToken(text, "encoding", "mojibake"))
            {
                if (path.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase) || path.Equals(".gitattributes", StringComparison.OrdinalIgnoreCase) || path.EndsWith("encodingGuard.test.js", StringComparison.OrdinalIgnoreCase) || path.EndsWith("encoding-precommit-check.js", StringComparison.OrdinalIgnoreCase))
                {
                    boost += 1.4; reasons.Add("task-encoding");
                }
            }
            return boost;
        }

        private static bool ContainsAny(string text, string fileName, string fileStem)
        {
            return text.Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '(', ')', '[', ']', '{', '}', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(tok => tok.Length >= 3 && (ContainsToken(fileName, tok) || ContainsToken(fileStem, tok)));
        }

        private static bool ContainsAnyToken(string text, params string[] tokens) => tokens.Any(t => ContainsToken(text, t));

        private static bool ContainsToken(string haystack, string token)
        {
            if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(token))
                return false;
            return haystack.Contains(token, StringComparison.OrdinalIgnoreCase);
        }
    }
}
