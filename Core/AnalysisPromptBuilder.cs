namespace LocalCursorAgent.Core
{
    internal static class AnalysisPromptBuilder
    {
        public static string BuildAnalysisPromptWithContext(string task, int iteration, string previousResponse, string compactContext, string responseLanguageRule)
        {
            return $@"You are a C# project analysis agent.

TASK:
{task}

RULES:
{responseLanguageRule}
- This is an analysis-only task.
- Do not use any tool.
- Do not ask for more files.
- Do not propose tool calls.
- Answer directly in concise natural language.
- Use only the provided indexed context.
- If the context is partial, explicitly say that the answer is based on indexed key files.

INDEXED PROJECT CONTEXT:
{compactContext}

{(iteration > 0 && !string.IsNullOrWhiteSpace(previousResponse) ? $"Previous result:\n{previousResponse}\n" : string.Empty)}

Write the final project overview now.";
        }
    }
}
