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
            "удали", "обнови", "пофикси", "ошибк", "сломано", "тест", "script", "package.json", ".cs", ".md", ".txt", "почини проект",
            "найди ошиб", "устрани", "разберись", "исследуй и исправь", "исправь и проверь", "исправь проблему",
            "run doctor", "прогони doctor", "запусти doctor", "сделай preflight", "добавь preflight", "проверь health-check",
            "добавь regression", "расшири intent словарь", "обнови intent словарь", "добавь фразы в intent", "intent словарь", "intent classifier", "почини clari", "сними тупик clarification",
            "добавь примеры формулировки", "перед run проверь backend path", "проверь backendprojectpath", "добавь repository в package.json",
            "сними тупик", "clarification required", "ux"
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

            if (executeScore >= 1 && !value.Contains('?') && !value.Contains('？') && !IsVagueExecutionRequest(value))
                return TaskIntentKind.Execute;

            if (executeScore >= 1 && (value.Contains("проект", StringComparison.Ordinal) || value.Contains("project", StringComparison.Ordinal)))
                return TaskIntentKind.Execute;

            if (executeScore == 0 && (value.Contains('?') || value.Contains('？')))
                return TaskIntentKind.Chat;

            if (chatScore >= 1 && executeScore == 0)
                return TaskIntentKind.Chat;

            return TaskIntentKind.Clarify;
        }

        private static bool IsVagueExecutionRequest(string value)
        {
            return value == "сделай нормально" ||
                   value == "почини" ||
                   value == "оно не работает" ||
                   value == "абракадабра" ||
                   value == "разберись" ||
                   value == "улучши всё";
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
