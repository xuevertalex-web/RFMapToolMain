namespace LocalCursorAgent.LLM
{
    public interface ILLMClient
    {
        Task<string> Generate(string prompt, CancellationToken cancellationToken = default);
        Task<bool> IsAvailable(CancellationToken cancellationToken = default);
    }
}
