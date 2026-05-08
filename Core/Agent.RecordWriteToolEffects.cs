using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private async Task RecordWriteToolEffectsAsync(
            string task,
            List<ToolCaller.ToolCall> toolCalls,
            List<string> resolvedFiles,
            HashSet<string> changedFiles,
            Dictionary<string, ChangedHint> changedHints,
            Dictionary<string, ChangedRange> changedRanges,
            Dictionary<string, ChangedKind> changedKinds,
            ExecutionTracer tracer)
        {
            foreach (var call in toolCalls)
            {
                if (!call.ToolName.Equals("file", StringComparison.OrdinalIgnoreCase) ||
                    !call.Input.StartsWith("write:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var filePath = WriteTargetPathExtractor.ExtractWriteTargetPath(call.Input);
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    continue;
                }

                changedFiles.Add(filePath);
                tracer.MarkChangedFile(filePath);
                var patchDecision = PatchDecisionBuilder.BuildPatchDecision(filePath, call.Input, resolvedFiles);
                _contextBuilder.Tracer.LogPatchDecision(patchDecision);
                changedHints[filePath] = ChangedHintComposer.BuildChangedHint(filePath, call.Input, patchDecision);
                var changedRange = ChangedRangeResolver.BuildChangedRange(filePath, call.Input, patchDecision, _projectIndexer.SymbolDirectory);
                if (changedRange != null)
                {
                    changedRanges[filePath] = changedRange;
                }

                changedKinds[filePath] = ChangedKindBuilder.BuildChangedKind(task, call.Input, patchDecision, buildResult: null);
                _fileStateManager.MarkHot(filePath);
                await _projectIndexer.ReindexFile(filePath);
            }
        }
    }
}
