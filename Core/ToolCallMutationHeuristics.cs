namespace LocalCursorAgent.Core
{
    internal static class ToolCallMutationHeuristics
    {
        public static bool IsMutationLikeToolCall(ToolCaller.ToolCall call)
        {
            var toolName = call?.ToolName ?? string.Empty;
            var input = call?.Input ?? string.Empty;

            if (!toolName.Equals("file", StringComparison.OrdinalIgnoreCase))
                return false;

            return input.StartsWith("write:", StringComparison.OrdinalIgnoreCase) ||
                   input.StartsWith("patch:", StringComparison.OrdinalIgnoreCase) ||
                   input.StartsWith("edit:", StringComparison.OrdinalIgnoreCase) ||
                   input.StartsWith("change:", StringComparison.OrdinalIgnoreCase);
        }
    }
}
