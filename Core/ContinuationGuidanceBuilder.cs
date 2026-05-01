namespace LocalCursorAgent.Core
{
    internal static class ContinuationGuidanceBuilder
    {
        public static string BuildContinuationHint(bool planRequired, string reasonCode, string lastKnownAction)
        {
            if (planRequired)
                return "Provide a step-by-step implementation plan and execute the first concrete edit.";

            if (string.Equals(reasonCode, "MAX_ITERATIONS_REACHED", StringComparison.OrdinalIgnoreCase))
                return "Continue from the last successful step and focus on one concrete blocking issue.";

            if (!string.IsNullOrWhiteSpace(lastKnownAction))
                return $"Continue from: {lastKnownAction}";

            return string.Empty;
        }

        public static string[] BuildNextActionCandidates(bool planRequired, string reasonCode, string continuationHint, string lastKnownAction)
        {
            var items = new List<string>();
            if (planRequired)
            {
                items.Add("Draft a 3-step implementation plan.");
                items.Add("Select the first target file and symbol.");
                items.Add("Apply one concrete edit and rerun verification.");
            }
            else if (string.Equals(reasonCode, "MAX_ITERATIONS_REACHED", StringComparison.OrdinalIgnoreCase))
            {
                items.Add("Focus on one unresolved blocker from the previous run.");
                items.Add("Apply one narrow fix and verify immediately.");
            }
            else if (!string.IsNullOrWhiteSpace(lastKnownAction))
            {
                items.Add($"Continue from previous action: {lastKnownAction}");
            }

            if (!string.IsNullOrWhiteSpace(continuationHint) && items.Count < 3)
                items.Add(continuationHint);

            return items
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToArray();
        }
    }
}
