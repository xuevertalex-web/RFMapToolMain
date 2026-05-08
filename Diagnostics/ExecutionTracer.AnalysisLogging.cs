using System.Collections.Generic;

namespace LocalCursorAgent.Diagnostics
{
    public partial class ExecutionTracer
    {
        #region Scoring Breakdown

        public void LogScoringBreakdown(ScoringBreakdown breakdown)
        {
            _scoringBreakdowns.Add(breakdown);
        }

        #endregion

        #region Memory Influence

        public void LogMemoryInfluence(MemoryInfluence influence)
        {
            _memoryInfluences.Add(influence);
        }

        #endregion

        #region Patch Decision

        public void LogPatchDecision(PatchDecision decision)
        {
            _patchDecisions.Add(decision);
            var summary = $"file={decision.TargetFile}; scope={decision.Scope}; risk={decision.RiskLevel}; reason={decision.Reason}";
            LogEvent("PatchDecision", "Patch scope selected", new Dictionary<string, object>
            {
                { "TargetFile", decision.TargetFile },
                { "TargetMethod", decision.TargetMethod },
                { "Scope", decision.Scope },
                { "Reason", decision.Reason },
                { "RiskLevel", decision.RiskLevel },
                { "Summary", summary },
                { "AlternativeFiles", decision.AlternativeFiles.ToArray() }
            });
        }

        #endregion

        #region Memory Update

        public void LogMemoryUpdate(MemoryUpdate update)
        {
            _memoryUpdates.Add(update);
        }

        #endregion
    }
}
