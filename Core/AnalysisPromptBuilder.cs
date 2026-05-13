namespace LocalCursorAgent.Core
{
    internal static class AnalysisPromptBuilder
    {
        private static readonly string[] DeepAnalysisTerms = new[]
        {
            "security", "audit", "vulnerability", "exploit", "bypass", "permission", "approval",
            "sandbox", "workspace boundary", "path traversal", "command execution", "deep analysis",
            "full analysis", "review architecture",
            "\u0443\u044f\u0437\u0432\u0438\u043c\u043e\u0441\u0442\u044c", // уязвимость
            "\u0430\u0443\u0434\u0438\u0442", // аудит
            "\u0431\u0435\u0437\u043e\u043f\u0430\u0441\u043d\u043e\u0441\u0442\u044c", // безопасность
            "\u043e\u0431\u0445\u043e\u0434", // обход
            "\u0441\u043b\u043e\u043c\u0430\u0442\u044c", // сломать
            "\u0434\u044b\u0440\u0430" // дыра
        };

        public static bool IsDeepAnalysisTask(string task)
        {
            var value = (task ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(value))
                return false;
            foreach (var term in DeepAnalysisTerms)
            {
                if (value.Contains(term, StringComparison.Ordinal))
                    return true;
            }
            return false;
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
