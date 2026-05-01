namespace LocalCursorAgent.Core
{
    internal static class TaskIntentClassifier
    {
        public static bool IsBroadEngineeringIntent(string task)
        {
            var value = (task ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.Contains("implement") ||
                   value.Contains("build ") ||
                   value.Contains("create ") ||
                   value.Contains("converter") ||
                   value.Contains("поэтап") ||
                   value.Contains("разбор") ||
                   value.Contains("приступ");
        }

        public static bool IsTechnicalAnalysisIntent(string task)
        {
            var value = (task ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.Contains("analy") ||
                   value.Contains("analysis") ||
                   value.Contains("synchron") ||
                   value.Contains("coordinate") ||
                   value.Contains("client") ||
                   value.Contains("server") ||
                   value.Contains("logic") ||
                   value.Contains("mechanism") ||
                   value.Contains("разбор") ||
                   value.Contains("анализ") ||
                   value.Contains("синхрон") ||
                   value.Contains("координат") ||
                   value.Contains("клиент") ||
                   value.Contains("сервер") ||
                   value.Contains("механизм");
        }
    }
}
