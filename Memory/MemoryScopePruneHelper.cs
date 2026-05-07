namespace LocalCursorAgent.Memory
{
    public static class MemoryScopePruneHelper
    {
        public static int PruneStaleScopeRecords(AgentMemorySystem memory, string projectScope)
        {
            return memory.InvalidateLowConfidenceRecords(projectScope);
        }
    }
}
