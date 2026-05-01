namespace LocalCursorAgent.Core
{
    internal static class MutationIntentDetector
    {
        public static bool IsMutationIntentTask(string task)
        {
            if (string.IsNullOrWhiteSpace(task))
                return false;

            var normalized = task.ToLowerInvariant();
            return normalized.Contains("fix") ||
                   normalized.Contains("create") ||
                   normalized.Contains("generate") ||
                   normalized.Contains("write") ||
                   normalized.Contains("make") ||
                   normalized.Contains("change") ||
                   normalized.Contains("update") ||
                   normalized.Contains("add") ||
                   normalized.Contains("implement") ||
                   normalized.Contains("modify") ||
                   normalized.Contains("refactor") ||
                   normalized.Contains("создай") ||
                   normalized.Contains("создать") ||
                   normalized.Contains("добавь") ||
                   normalized.Contains("добавить") ||
                   normalized.Contains("измени") ||
                   normalized.Contains("исправь") ||
                   normalized.Contains("напиши") ||
                   normalized.Contains("сделай") ||
                   normalized.Contains("файл") ||
                   normalized.Contains("класс") ||
                   normalized.Contains("ensure build passes");
        }
    }
}
