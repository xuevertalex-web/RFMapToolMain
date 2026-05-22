using System;
using System.Collections.Generic;
using System.Linq;

namespace RFMapToolSharp.Tools;

public sealed class SafePatchDryRunPlan
{
    public List<string> PlannedNodes { get; init; } = new();
    public List<string> AllowedNonBspNodes { get; init; } = new();
    public List<string> RejectedBspNodes { get; init; } = new();
    public List<string> RejectedDummyNodes { get; init; } = new();
    public bool WouldPatchExistingGeometry { get; init; }
    public bool WouldChangeExistingVertexPool { get; init; }
    public string? AbortReason { get; set; }
}

public static class SafePatchIsolationGuard
{
    public static SafePatchDryRunPlan BuildPlan(IEnumerable<string> plannedNodes, IEnumerable<string> allowedNonBspNodeNames)
    {
        var allow = new HashSet<string>(allowedNonBspNodeNames.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
        var plan = new SafePatchDryRunPlan();
        foreach (var raw in plannedNodes)
        {
            var name = string.IsNullOrWhiteSpace(raw) ? "noname" : raw.Trim();
            plan.PlannedNodes.Add(name);
            if (name.StartsWith("BSP_mg", StringComparison.OrdinalIgnoreCase))
            {
                plan.RejectedBspNodes.Add(name);
                continue;
            }
            if (name.StartsWith("dummy", StringComparison.OrdinalIgnoreCase))
            {
                plan.RejectedDummyNodes.Add(name);
                continue;
            }
            if (allow.Contains(name))
            {
                plan.AllowedNonBspNodes.Add(name);
            }
        }
        return plan;
    }

    public static string? EvaluateAbortReason(SafePatchDryRunPlan plan, int patchedMgCount, bool wouldChangeExistingVertexPool)
    {
        if (plan.WouldPatchExistingGeometry)
            return "strict_nonbsp_guard: existing BSP_mg geometry would be patched";
        if (wouldChangeExistingVertexPool || plan.WouldChangeExistingVertexPool)
            return "strict_nonbsp_guard: existing BSP vertex pool would change";
        if (patchedMgCount > plan.AllowedNonBspNodes.Count)
            return "strict_nonbsp_guard: PATCHED_MG exceeds allowed non-BSP node count";
        return null;
    }
}
