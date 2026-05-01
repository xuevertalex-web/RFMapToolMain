namespace LocalCursorAgent.Core
{
    internal static class ContextPromptBuilder
    {
        public static string BuildPromptWithContext(
            string task,
            int iteration,
            string previousResponse,
            string codeContext,
            string regressionAdvice,
            string promptShapingAdvice,
            string strategyBiasAdvice,
            string executionContext,
            string taskProfile,
            string toolsDescription,
            string policyBlock,
            string startupStateBlock,
            string responseLanguageRule)
        {
            return $@"You are a skilled C# coding agent with semantic understanding.

TASK:
{task}

IMPORTANT GUIDELINES:
{responseLanguageRule}
- Create new files directly in the workspace when the task asks for something new.
- Creating new folders and files inside the workspace is allowed.
- Do not ask the user for a path; choose an appropriate file name yourself.
- If the user asks for a simple calculator without specifying a path, create Calculator.cs in the workspace root.
- Modify ONLY necessary code
- Keep structure intact
- Make targeted, minimal changes
- Never rewrite entire files blindly
- Focus on correctness and compilation
- If the task is analysis-only, explanation-only, or diagnosis-only, answer directly without any tool call
- If you need to use a tool, use only one tool call per iteration
- Never invent new tool names or tool modes
- The only valid tool names are exactly the names listed below

RELEVANT CODE:
{codeContext}

{policyBlock}

{startupStateBlock}

Available tools:
{toolsDescription}

TOOL FORMAT:
TOOL: tool_name
INPUT: command_here

TOOL USAGE RULES:
- Use TOOL: file for read/write/delete/rename/move operations
- Use TOOL: build for build or verification operations
- For build, pass the workspace root or a path inside the workspace; do not pass a solution file as the working directory
- Do not emit any other TOOL name
- Do not emit multiple tool calls in one response
- Do not wrap tool calls in markdown fences

Examples:
TOOL: file
INPUT: read:MyClass.cs

TOOL: file
INPUT: write:MyClass.cs:using System;

public class MyClass {{ /* implementation */ }}

Only use ONE tool call per iteration. If you are not sure which tool to use, prefer a plain natural-language response instead of guessing a new tool name.

{(iteration > 0 && !string.IsNullOrWhiteSpace(previousResponse) ? $"Previous result:\n{previousResponse}\n" : string.Empty)}

{(string.IsNullOrWhiteSpace(taskProfile) ? string.Empty : $"Task profile:\n{taskProfile}\n")}

{(string.IsNullOrWhiteSpace(regressionAdvice) ? string.Empty : $"{regressionAdvice}\n")}

{(string.IsNullOrWhiteSpace(promptShapingAdvice) ? string.Empty : $"{promptShapingAdvice}\n")}

{(string.IsNullOrWhiteSpace(strategyBiasAdvice) ? string.Empty : $"{strategyBiasAdvice}\n")}

{(executionContext.Length > 0 ? $"Execution history:\n{executionContext}\n" : string.Empty)}

What is your next step?";
        }
    }
}
