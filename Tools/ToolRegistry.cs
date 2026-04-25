namespace LocalCursorAgent.Tools
{
    /// <summary>
    /// Registry for managing all available tools.
    /// </summary>
    public class ToolRegistry
    {
        private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Register a tool in the registry.
        /// </summary>
        public void Register(ITool tool)
        {
            if (tool == null)
                return;

            _tools[tool.Name] = tool;
        }

        /// <summary>
        /// Get a tool by name.
        /// </summary>
        public ITool? GetTool(string name)
        {
            return _tools.TryGetValue(name, out var tool) ? tool : null;
        }

        /// <summary>
        /// Check if a tool exists by name.
        /// </summary>
        public bool HasTool(string name)
        {
            return _tools.ContainsKey(name);
        }

        /// <summary>
        /// Get all registered tools.
        /// </summary>
        public IEnumerable<ITool> GetAllTools()
        {
            return _tools.Values;
        }

        /// <summary>
        /// Get a list of all tool names and descriptions.
        /// </summary>
        public string GetToolsDescription()
        {
            var descriptions = _tools.Values
                .Select(t => $"- {t.Name}: {t.Description}")
                .ToList();

            return string.Join("\n", descriptions);
        }
    }
}
