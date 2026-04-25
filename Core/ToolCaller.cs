using LocalCursorAgent.Tools;

namespace LocalCursorAgent.Core
{
    /// <summary>
    /// Parses and executes tool calls from LLM responses.
    /// </summary>
    public class ToolCaller
    {
        private readonly ToolRegistry _toolRegistry;

        public class ToolCall
        {
            public string ToolName { get; set; } = string.Empty;
            public string Input { get; set; } = string.Empty;
        }

        public ToolCaller(ToolRegistry toolRegistry)
        {
            _toolRegistry = toolRegistry;
        }

        /// <summary>
        /// Parse tool calls from LLM response text.
        /// Expected format:
        /// TOOL: tool_name
        /// INPUT: command_here
        /// </summary>
        public List<ToolCall> ParseToolCalls(string response)
        {
            var toolCalls = new List<ToolCall>();

            if (string.IsNullOrWhiteSpace(response))
                return toolCalls;

            var lines = response.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (line.StartsWith("TOOL:", StringComparison.OrdinalIgnoreCase))
                {
                    var toolName = line.Substring(5).Trim();

                    // Look for INPUT on next line
                    if (i + 1 < lines.Length)
                    {
                        var nextLine = lines[i + 1].Trim();
                        if (nextLine.StartsWith("INPUT:", StringComparison.OrdinalIgnoreCase))
                        {
                            var inputLines = new List<string>
                            {
                                nextLine.Substring(6).Trim()
                            };

                            var j = i + 2;
                            while (j < lines.Length)
                            {
                                var candidate = lines[j];
                                if (candidate.TrimStart().StartsWith("TOOL:", StringComparison.OrdinalIgnoreCase))
                                    break;

                                inputLines.Add(candidate);
                                j++;
                            }

                            var input = string.Join(Environment.NewLine, inputLines).TrimEnd();
                            toolCalls.Add(new ToolCall { ToolName = toolName, Input = input });
                            i = j - 1;
                        }
                    }
                }
            }

            return toolCalls;
        }

        /// <summary>
        /// Execute a list of tool calls and return results.
        /// </summary>
        public async Task<List<string>> ExecuteToolCalls(List<ToolCall> toolCalls)
        {
            var results = new List<string>();

            foreach (var call in toolCalls)
            {
                var result = await ExecuteToolCall(call);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Execute a single tool call.
        /// </summary>
        public async Task<string> ExecuteToolCall(ToolCall toolCall)
        {
            if (string.IsNullOrWhiteSpace(toolCall.ToolName))
                return "Error: Tool name is empty";

            var tool = _toolRegistry.GetTool(toolCall.ToolName);
            if (tool == null)
                return $"Error: Tool '{toolCall.ToolName}' not found";

            var result = await tool.Execute(toolCall.Input);
            return result;
        }

        /// <summary>
        /// Check if response contains any tool calls.
        /// </summary>
        public bool ContainsToolCalls(string response)
        {
            return ParseToolCalls(response).Count > 0;
        }
    }
}
