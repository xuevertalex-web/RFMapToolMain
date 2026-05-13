namespace LocalCursorAgent.Core
{
    internal static class AnalysisPromptBuilder
    {
        internal const int NormalAnalysisFileBudget = 4;
        internal const int DeepAnalysisFileBudget = 12;

        internal readonly struct DeepAnalysisDecision(bool isDeep, string trigger)
        {
            public bool IsDeep { get; } = isDeep;
            public string Trigger { get; } = trigger;
        }

        private static readonly string[] DeepAnalysisTerms = new[]
        {
            "security", "audit", "vulnerability", "exploit", "bypass", "permission", "approval",
            "sandbox", "workspace boundary", "path traversal", "command execution", "deep analysis",
            "full analysis", "review architecture", "vsix", "install", "stale", "package", "workflow", "update",
            "\u0440\u0430\u0441\u0448\u0438\u0440\u0435\u043d\u0438\u0435", "\u0443\u0441\u0442\u0430\u043d\u043e\u0432\u043a\u0430", "\u043f\u0430\u043a\u0435\u0442",
            "\u0443\u044f\u0437\u0432\u0438\u043c\u043e\u0441\u0442\u044c", // уязвимость
            "\u0430\u0443\u0434\u0438\u0442", // аудит
            "\u0431\u0435\u0437\u043e\u043f\u0430\u0441\u043d\u043e\u0441\u0442\u044c", // безопасность
            "\u043e\u0431\u0445\u043e\u0434", // обход
            "\u0441\u043b\u043e\u043c\u0430\u0442\u044c", // сломать
            "\u0434\u044b\u0440\u0430" // дыра
        };

        private static readonly string[] RiskNouns = new[]
        {
            "risk", "unsafe", "break", "hole", "weak spot", "failure mode", "attack path", "abuse path",
            "\u0441\u043b\u043e\u043c\u0430\u0442\u044c", "\u0440\u0438\u0441\u043a", "\u0434\u044b\u0440\u0430", "\u0441\u043b\u0430\u0431\u043e\u0435 \u043c\u0435\u0441\u0442\u043e", "\u0441\u043b\u0430\u0431", "\u0447\u0442\u043e \u043c\u043e\u0436\u043d\u043e \u043e\u0431\u043e\u0439\u0442\u0438", "\u043e\u0431\u043e\u0439\u0442\u0438"
        };

        private static readonly string[] SecuritySurfaceTerms = new[]
        {
            "command", "file", "workspace", "approval", "token", "permission", "sandbox", "path", "process", "shell", "guard",
            "\u043a\u043e\u043c\u0430\u043d\u0434\u0430", "\u043a\u043e\u043c\u0430\u043d\u0434", "\u0444\u0430\u0439\u043b", "\u0440\u0430\u0431\u043e\u0447\u0430\u044f \u043e\u0431\u043b\u0430\u0441\u0442\u044c", "\u0440\u0430\u0437\u0440\u0435\u0448\u0435\u043d\u0438\u0435", "\u0440\u0430\u0437\u0440\u0435\u0448\u0435\u043d", "\u0442\u043e\u043a\u0435\u043d", "sandbox", "\u043f\u0443\u0442\u044c", "\u043f\u0440\u043e\u0446\u0435\u0441\u0441", "shell", "guard"
        };

        public static DeepAnalysisDecision EvaluateDeepAnalysisTask(string task)
        {
            var value = (task ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(value))
                return new DeepAnalysisDecision(false, "none");

            if (DeepAnalysisTerms.Any(term => value.Contains(term, StringComparison.Ordinal)))
                return new DeepAnalysisDecision(true, "keyword");

            var hasRiskNoun = RiskNouns.Any(term => value.Contains(term, StringComparison.Ordinal));
            var hasSurfaceTerm = SecuritySurfaceTerms.Any(term => value.Contains(term, StringComparison.Ordinal));
            if (hasRiskNoun && hasSurfaceTerm)
                return new DeepAnalysisDecision(true, "risk-combination");

            return new DeepAnalysisDecision(false, "none");
        }

        public static bool IsDeepAnalysisTask(string task)
        {
            return EvaluateDeepAnalysisTask(task).IsDeep;
        }

        public static string BuildAnalysisPromptWithContext(string task, int iteration, string previousResponse, string compactContext, string responseLanguageRule)
        {
            var deepAnalysis = IsDeepAnalysisTask(task);
            var analysisRules = deepAnalysis
                ? @"- This is an analysis-only task.
- Do not use any tool.
- Do not make code changes.
- Do not ask for more files.
- Do not propose tool calls.
- Inspect implementation details in the provided code context.
- Report concrete findings with:
  1) severity,
  2) affected files,
  3) reproducibility idea,
  4) fix direction.
- Distinguish confirmed issues from hypotheses.
- If context is partial, explicitly state uncertainty boundaries."
                : @"- This is an analysis-only task.
- Do not use any tool.
- Do not make code changes.
- Do not ask for more files.
- Do not propose tool calls.
- Answer directly in concise natural language.
- Use only the provided indexed context.
- If the context is partial, explicitly say that the answer is based on indexed key files.";

            return $@"You are a C# project analysis agent.

TASK:
{task}

RULES:
{responseLanguageRule}
{analysisRules}

INDEXED PROJECT CONTEXT:
{compactContext}

{(iteration > 0 && !string.IsNullOrWhiteSpace(previousResponse) ? $"Previous result:\n{previousResponse}\n" : string.Empty)}

Write the final project overview now.";
        }
    }
}
