using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Indexing;

namespace LocalCursorAgent.Core
{
    internal static class ChangedRangeResolver
    {
        internal static Agent.ChangedRange? BuildChangedRange(string filePath, string toolInput, ExecutionTracer.PatchDecision patchDecision, ProjectSymbolDirectory? symbolDirectory)
        {
            var lines = AgentSymbolRangeSupport.TryReadAllLines(filePath);
            if (lines is null)
            {
                return null;
            }

            var candidates = AgentSymbolRangeSupport.BuildChangedRangeCandidates(filePath, toolInput, patchDecision.TargetMethod);
            var indexedSymbols = AgentSymbolRangeSupport.GetIndexedSymbolsOrEmpty(symbolDirectory, filePath);
            var uniqueCandidates = AgentSymbolRangeSupport.DistinctIgnoreCase(candidates);

            foreach (var candidate in uniqueCandidates)
            {
                var symbolRange = FindBestSymbolRangeForFile(lines, indexedSymbols, candidate);
                if (!symbolRange.HasValue)
                {
                    continue;
                }

                var (startLine, endLine) = symbolRange.Value;
                var changedRangeData = AgentSymbolRangeSupport.CreateChangedRangeData(filePath, startLine, endLine);
                return new Agent.ChangedRange
                {
                    File = changedRangeData.filePath,
                    StartLine = changedRangeData.startLine,
                    EndLine = changedRangeData.endLine
                };
            }

            foreach (var candidate in uniqueCandidates)
            {
                var lineIndex = AgentSymbolRangeSupport.FindMatchingLine(lines, candidate);
                if (lineIndex < 0)
                {
                    continue;
                }

                var enclosingRange = AgentSymbolRangeSupport.FindNearestEnclosingSymbolRange(lines, lineIndex);
                if (enclosingRange is not null)
                {
                    var enclosingRangeData = AgentSymbolRangeSupport.CreateChangedRangeData(filePath, enclosingRange.Value.startLine, enclosingRange.Value.endLine);
                    return new Agent.ChangedRange
                    {
                        File = enclosingRangeData.filePath,
                        StartLine = enclosingRangeData.startLine,
                        EndLine = enclosingRangeData.endLine
                    };
                }

                var startLine = AgentSymbolRangeSupport.ToOneBasedLineNumber(lineIndex);
                var singleLineRangeData = AgentSymbolRangeSupport.CreateChangedRangeData(filePath, startLine, startLine);
                return new Agent.ChangedRange
                {
                    File = singleLineRangeData.filePath,
                    StartLine = singleLineRangeData.startLine,
                    EndLine = singleLineRangeData.endLine
                };
            }

            return null;
        }

        private static (int startLine, int endLine)? FindBestSymbolRangeForFile(string[] lines, List<string> indexedSymbols, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate) || lines.Length is 0)
            {
                return null;
            }

            var searchOrder = AgentSymbolRangeSupport.BuildSearchOrder(indexedSymbols, candidate);
            foreach (var symbol in searchOrder)
            {
                var symbolLine = AgentSymbolRangeSupport.FindSymbolDeclarationLine(lines, symbol);
                if (symbolLine < 0)
                {
                    continue;
                }

                var methodStart = AgentSymbolRangeSupport.FindNearestDeclarationStart(lines, anchorLineIndex: symbolLine, declarationKind: "method");
                if (methodStart >= 0)
                {
                    return AgentSymbolRangeSupport.BuildBlockRangeFromDeclaration(lines, methodStart);
                }

                var classStart = AgentSymbolRangeSupport.FindNearestDeclarationStart(lines, anchorLineIndex: symbolLine, declarationKind: "class");
                if (classStart >= 0)
                {
                    return AgentSymbolRangeSupport.BuildBlockRangeFromDeclaration(lines, classStart);
                }

                var startLine = AgentSymbolRangeSupport.ToOneBasedLineNumber(symbolLine);
                return (startLine, startLine);
            }

            return null;
        }
    }
}
