using LocalCursorAgent.Context;
using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.LLM;
using LocalCursorAgent.LLM.Runtime;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class RunContext
        {
            public RunContext(string task) { Task = task; }
            public string Task { get; }
            public DateTime? RunStartedUtc { get; set; }
            public ExecutionTracer? Tracer { get; set; }
            public string? RequestedNewFile { get; set; }
            public bool AnalysisOnlyTask { get; set; }
            public ILlmRuntimeClient? RuntimeClient { get; set; }
            public LlmProviderMetadata? RuntimeMetadata { get; set; }
            public TargetResolutionGateResult? TargetResolution { get; set; }
            public List<string>? GatedTargetFiles { get; set; }
            public AgentRunState RunState { get; } = new();
            public HashSet<string> ChangedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, ChangedHint> ChangedHints { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, ChangedRange> ChangedRanges { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, ChangedKind> ChangedKinds { get; } = new(StringComparer.OrdinalIgnoreCase);
            public List<string>? ResolvedFiles { get; set; }
            public ContextInformation? ContextInfo { get; set; }
            public LlmRuntimeResult RuntimeResult { get; set; }
            public ToolingStateApplyResult? ToolingApply { get; set; }
        }

        private readonly record struct RunStageResult(bool ShouldReturn, string? FinalResult)
        {
            public static RunStageResult Continue() => new(false, null);
            public static RunStageResult Return(string result) => new(true, result);
        }

        private readonly record struct MutationStageResult(bool ShouldContinue);
    }
}
