namespace LocalCursorAgent.Memory
{
    public static class MemoryInvalidationHook
    {
        public static int RecalibrateAndPruneOnFailure(AgentMemorySystem memory, string projectScope)
        {
            _ = memory.RecalibrateConfidenceByProjectScope(projectScope, success: false);
            return memory.InvalidateLowConfidenceRecords(projectScope);
        }
    }
}
