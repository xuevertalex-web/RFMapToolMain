namespace LocalCursorAgent.Core
{
    internal static class MutationIntentDetector
    {
        public static bool IsMutationIntentTask(string task)
        {
            return TaskIntentScorer.Classify(task) == TaskIntentKind.Execute;
        }
    }
}
