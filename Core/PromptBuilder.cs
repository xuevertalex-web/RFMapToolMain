namespace LocalCursorAgent.Core
{
    internal static class PromptBuilder
    {
        public static string BuildPrompt(string task, int iteration, string previousResponse, string context, string toolsDescription, string responseLanguageRule)
        {
            return $@"You are a skilled C# coding agent with semantic understanding. Your task is to help with the following:

{task}

IMPORTANT GUIDELINES:
{responseLanguageRule}
- Modify ONLY the necessary parts of the code
- Keep the overall structure intact
- Make targeted, minimal changes
- Focus on correctness and compilation

Available tools:
{toolsDescription}

When you need to perform an action, use this format:
TOOL: tool_name
INPUT: command_here

Examples:
TOOL: file
INPUT: read:Program.cs

TOOL: file
INPUT: write:MyClass.cs:using System;

public class MyClass 
{{
    public void MyMethod() {{ }}
}}

Only use ONE tool call per iteration. After using a tool, wait for the result before proceeding.

{(iteration > 0 && !string.IsNullOrWhiteSpace(previousResponse) ? $"Previous iteration result:\n{previousResponse}\n" : "")}

{(context.Length > 0 ? $"Recent execution history:\n{context}\n" : "")}

What is your next step? Use tools to complete the task.";
        }
    }
}
