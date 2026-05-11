namespace LocalCursorAgent.Core
{
    public enum TaskPlanMode
    {
        Analysis,
        Execute,
        Clarify
    }

    public sealed class TaskPlan
    {
        public TaskPlanMode Mode { get; init; }
        public List<string> Steps { get; init; } = new();
        public List<string> TargetZones { get; init; } = new();
        public List<string> TargetRoles { get; init; } = new();
        public List<string> CandidateFiles { get; init; } = new();
        public List<string> Risks { get; init; } = new();
        public List<string> Checks { get; init; } = new();
        public List<string> StopConditions { get; init; } = new();
        public double Confidence { get; init; }
        public string Reason { get; init; } = string.Empty;
    }
}
