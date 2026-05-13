using LocalCursorAgent.Context;

namespace LocalCursorAgent.Core
{
    internal static class AnalysisContextFormatter
    {
        public static string BuildCompactAnalysisContext(ContextInformation contextInfo)
        {
            if (contextInfo.SelectedFiles.Count == 0)
                return "No indexed files were selected for this analysis task.";

            var lines = new List<string>();
            foreach (var file in contextInfo.SelectedFiles.Take(4))
            {
                lines.Add($"FILE: {file}");
                if (contextInfo.RelevantSymbols.TryGetValue(file, out var symbols) && symbols.Count > 0)
                    lines.Add($"SYMBOLS: {string.Join(", ", symbols.Take(6))}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        public static string BuildDeepAnalysisContext(ContextInformation contextInfo)
        {
            if (contextInfo.SelectedFiles.Count == 0)
                return "No indexed files were selected for this analysis task.";

            var lines = new List<string>();
            foreach (var file in contextInfo.SelectedFiles)
            {
                lines.Add($"// FILE: {file}");
                if (contextInfo.RelevantSymbols.TryGetValue(file, out var symbols) && symbols.Count > 0)
                    lines.Add($"// SYMBOLS: {string.Join(", ", symbols.Take(12))}");
                lines.Add("```");
                lines.Add(contextInfo.FileContents.TryGetValue(file, out var content) ? content : string.Empty);
                lines.Add("```");
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
