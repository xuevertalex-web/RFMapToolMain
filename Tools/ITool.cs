namespace LocalCursorAgent.Tools
{
    /// <summary>
    /// Interface for all tools that the agent can call.
    /// </summary>
    public interface ITool
    {
        /// <summary>
        /// Name of the tool (used for tool calling).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of what the tool does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Execute the tool with the given input.
        /// </summary>
        /// <param name="input">The input command for the tool</param>
        /// <returns>The result of executing the tool</returns>
        Task<string> Execute(string input);
    }
}
