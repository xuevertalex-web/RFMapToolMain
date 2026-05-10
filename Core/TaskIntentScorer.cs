namespace LocalCursorAgent.Core
{
    internal enum TaskIntentKind
    {
        Chat,
        Clarify,
        Execute
    }

    internal static class TaskIntentScorer
    {
        private static readonly string[] ExecuteMarkers = new[]
        {
            "fix", "create", "generate", "write", "make", "change", "update", "add", "implement", "modify", "refactor",
            "создай", "создать", "добавь", "добавить", "измени", "исправь", "почини", "напиши", "сделай", "файл", "класс",
            "удали", "обнови", "пофикси", "ошибк", "сломано", "тест", "script", "package.json", ".cs", ".md", ".txt", "почини проект"
        };

        private static readonly string[] ChatMarkers = new[]
        {
            "hi", "hello", "hey", "thanks", "thank you", "how are you", "what can you do",
            "привет", "спасибо", "ты тут", "тут", "а щас", "щас", "ок",
            "что ты умеешь", "объясни", "опиши проект", "что делает проект", "как дела", "расскажи", "какие риски", "что дальше лучше сделать"
        };

        public static TaskIntentKind Classify(string? task)
        {
            var value = (task ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(value))
                return TaskIntentKind.Clarify;

            var executeScore = ScoreByMarkers(value, ExecuteMarkers);
            var chatScore = ScoreByMarkers(value, ChatMarkers);

            if (executeScore >= 2)
                return TaskIntentKind.Execute;

            if (executeScore == 0 && (value.Contains('?') || value.Contains('？')))
                return TaskIntentKind.Chat;

            if (chatScore >= 1 && executeScore == 0)
                return TaskIntentKind.Chat;

            return TaskIntentKind.Clarify;
        }

        private static int ScoreByMarkers(string text, string[] markers)
        {
            var score = 0;
            foreach (var marker in markers)
            {
                if (text.Contains(marker, StringComparison.Ordinal))
                    score++;
            }

            return score;
        }
    }
}
