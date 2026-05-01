namespace LocalCursorAgent.Core
{
    internal static class ChangedKindClassifier
    {
        public static Agent.ChangedKindType ClassifyIntent(string task, string toolInput, string patchReason, Execution.BuildVerifier.BuildResult? buildResult)
        {
            var combined = string.Join(" ", new[] { task, toolInput, patchReason, buildResult?.Errors != null ? string.Join(" ", buildResult.Errors) : string.Empty })
                .ToLowerInvariant();

            if (combined.Contains("validation") || combined.Contains("null check") || combined.Contains("input check"))
                return Agent.ChangedKindType.Validation;
            if (combined.Contains("refactor") || combined.Contains("refined") || combined.Contains("rework"))
                return Agent.ChangedKindType.Refactor;
            if (combined.Contains("build error") || combined.Contains("compile") || combined.Contains("build failed") || combined.Contains("cs") || combined.Contains("restore"))
                return Agent.ChangedKindType.BuildFix;
            if (combined.Contains("fix") || combined.Contains("bug") || combined.Contains("error handling") || combined.Contains("exception"))
                return Agent.ChangedKindType.BugFix;
            if (combined.Contains("add") || combined.Contains("new") || combined.Contains("feature") || combined.Contains("implement"))
                return Agent.ChangedKindType.FeatureAdd;
            if (combined.Contains("update") || combined.Contains("adjust") || combined.Contains("change") || combined.Contains("modify"))
                return Agent.ChangedKindType.Update;

            return Agent.ChangedKindType.Unknown;
        }
    }
}
