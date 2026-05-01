namespace LocalCursorAgent.Core
{
    internal static class ActionLifecycleMapper
    {
        public static Agent.ActionLifecyclePayload[] MapActionLifecycle(IReadOnlyList<ActionLifecycleEntry> entries)
        {
            return entries.Select(e => new Agent.ActionLifecyclePayload
            {
                Sequence = e.Sequence,
                ActionCorrelationId = e.ActionCorrelationId,
                ActionType = e.ActionType,
                Target = e.Target,
                Command = e.Command,
                NormalizedTarget = e.NormalizedTarget,
                LifecycleState = e.LifecycleState.ToString(),
                ReasonCode = e.ReasonCode,
                Reason = e.Reason,
                ApprovalStatus = e.ApprovalStatus,
                IsInsideSandbox = e.IsInsideSandbox
            }).ToArray();
        }
    }
}
