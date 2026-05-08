using System.Linq;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private sealed class NormalizedChangedPayload
        {
            public required string[] Files { get; init; }
            public required ChangedHintPayload[] Hints { get; init; }
            public required ChangedRangePayload[] Ranges { get; init; }
            public required ChangedKindPayload[] Kinds { get; init; }
        }

        private static NormalizedChangedPayload NormalizeChangedPayload(
            IEnumerable<string> changedFiles,
            IEnumerable<ChangedHint> changedHints,
            IEnumerable<ChangedRange> changedRanges,
            IEnumerable<ChangedKind> changedKinds)
        {
            var normalizedChangedArtifacts = ChangedArtifactPayloadBuilder.Normalize(changedFiles, changedHints, changedRanges, changedKinds);
            return new NormalizedChangedPayload
            {
                Files = normalizedChangedArtifacts.Files,
                Hints = normalizedChangedArtifacts.Hints
                    .Select(h => new ChangedHintPayload
                    {
                        File = h.File,
                        Hint = h.Hint
                    })
                    .ToArray(),
                Ranges = normalizedChangedArtifacts.Ranges
                    .Select(r => new ChangedRangePayload
                    {
                        File = r.File,
                        StartLine = r.StartLine,
                        EndLine = r.EndLine > 0 ? r.EndLine : r.StartLine
                    })
                    .ToArray(),
                Kinds = normalizedChangedArtifacts.Kinds
                    .Select(k => new ChangedKindPayload
                    {
                        File = k.File,
                        Kind = k.Kind
                    })
                    .ToArray()
            };
        }
    }
}
