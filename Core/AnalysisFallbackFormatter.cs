using LocalCursorAgent.Context;

namespace LocalCursorAgent.Core
{
    internal static class AnalysisFallbackFormatter
    {
        public static string BuildAnalysisFallbackSummary(ContextInformation contextInfo, string fallbackReason)
        {
            var files = contextInfo.SelectedFiles.Take(10).ToList();
            var lines = new List<string> { "Краткий обзор проекта:" };
            if (files.Count == 0)
            {
                lines.Add("- Подходящие файлы не были выбраны из контекста.");
                lines.Add("- LLM недоступна, поэтому обзор ограничен индексированием проекта.");
                return string.Join(Environment.NewLine, lines);
            }

            lines.Add($"- По задаче выбрано {files.Count} ключевых файлов контекста.");

            var topFolders = files
                .Select(file => file.Contains(Path.DirectorySeparatorChar) || file.Contains(Path.AltDirectorySeparatorChar)
                    ? file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0]
                    : "(root)")
                .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .Select(g => $"{g.Key}: {g.Count()}")
                .ToList();

            if (topFolders.Count > 0)
                lines.Add($"- Основные области: {string.Join(", ", topFolders)}.");

            foreach (var file in files)
            {
                var symbols = contextInfo.RelevantSymbols.TryGetValue(file, out var relevantSymbols)
                    ? relevantSymbols.Take(5).ToList()
                    : new List<string>();
                var symbolSuffix = symbols.Count > 0
                    ? $" | Символы: {string.Join(", ", symbols)}"
                    : string.Empty;
                lines.Add($"- {file}{symbolSuffix}");
            }

            lines.Add(GetAnalysisFallbackReasonText(fallbackReason));
            return string.Join(Environment.NewLine, lines);
        }

        private static string GetAnalysisFallbackReasonText(string fallbackReason)
        {
            if (string.Equals(fallbackReason, "MODEL_TIMEOUT", StringComparison.OrdinalIgnoreCase))
                return "- Ответ собран из индексированного контекста, потому что локальная модель не завершила запрос вовремя.";

            if (string.Equals(fallbackReason, "PROVIDER_UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
                return "- Ответ собран из индексированного контекста, потому что локальная модель недоступна или не найдена.";

            return "- Ответ собран из индексированного контекста, потому что запрос к локальной модели завершился ошибкой.";
        }
    }
}
