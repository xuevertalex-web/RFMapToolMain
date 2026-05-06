namespace LocalCursorAgent.Core
{
    internal static class ChangedArtifactPayloadBuilder
    {
        internal static ChangedArtifactNormalizationResult Normalize(
            IEnumerable<string> changedFiles,
            IEnumerable<Agent.ChangedHint> changedHints,
            IEnumerable<Agent.ChangedRange> changedRanges,
            IEnumerable<Agent.ChangedKind> changedKinds)
        {
            var files = changedFiles
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var hints = changedHints
                .Where(h => h != null && !string.IsNullOrWhiteSpace(h.File) && !string.IsNullOrWhiteSpace(h.Hint))
                .GroupBy(h => h.File, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToArray();

            if (hints.Length == 0 && files.Length > 0)
            {
                hints = files.Select(file => new Agent.ChangedHint
                {
                    File = file,
                    Hint = "Updated by agent"
                }).ToArray();
            }

            var ranges = changedRanges
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.File) && r.StartLine > 0)
                .GroupBy(r => r.File, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToArray();

            var kinds = changedKinds
                .Where(k => k != null && !string.IsNullOrWhiteSpace(k.File))
                .GroupBy(k => k.File, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToArray();

            return new ChangedArtifactNormalizationResult
            {
                Files = files,
                Hints = hints,
                Ranges = ranges,
                Kinds = kinds
            };
        }
    }

    internal sealed class ChangedArtifactNormalizationResult
    {
        public string[] Files { get; init; } = Array.Empty<string>();
        public Agent.ChangedHint[] Hints { get; init; } = Array.Empty<Agent.ChangedHint>();
        public Agent.ChangedRange[] Ranges { get; init; } = Array.Empty<Agent.ChangedRange>();
        public Agent.ChangedKind[] Kinds { get; init; } = Array.Empty<Agent.ChangedKind>();
    }
}
