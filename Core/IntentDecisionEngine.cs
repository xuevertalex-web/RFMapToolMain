namespace LocalCursorAgent.Core
{
    internal enum UnifiedIntentKind
    {
        Chat,
        Clarify,
        Analysis,
        Execute
    }

    internal readonly record struct IntentDecision(
        UnifiedIntentKind Intent,
        double Confidence,
        string Reason,
        bool MutationAllowed,
        bool NeedsClarification);

    internal static class IntentDecisionEngine
    {
        public static IntentDecision Decide(string? task)
        {
            var value = (task ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return new IntentDecision(UnifiedIntentKind.Clarify, 0.95, "empty_task", MutationAllowed: false, NeedsClarification: true);
            }

            var normalized = value.ToLowerInvariant();

            if (ContainsAny(normalized, "без правок", "ничего не меняй", "только объясни"))
            {
                return new IntentDecision(UnifiedIntentKind.Analysis, 0.95, "explicit_no_mutation", MutationAllowed: false, NeedsClarification: false);
            }

            if (TaskPrecheckHeuristics.IsAnalysisOnlyTask(value))
            {
                return new IntentDecision(UnifiedIntentKind.Analysis, 0.90, "analysis_precheck", MutationAllowed: false, NeedsClarification: false);
            }

            if (IsBroadVagueMutation(normalized))
            {
                return new IntentDecision(UnifiedIntentKind.Clarify, 0.90, "broad_vague_mutation", MutationAllowed: false, NeedsClarification: true);
            }

            var scored = TaskIntentScorer.Classify(value);
            if (scored == TaskIntentKind.Chat)
            {
                return new IntentDecision(UnifiedIntentKind.Chat, 0.85, "task_intent_chat", MutationAllowed: false, NeedsClarification: false);
            }

            if (scored == TaskIntentKind.Clarify)
            {
                return new IntentDecision(UnifiedIntentKind.Clarify, 0.90, "task_intent_clarify", MutationAllowed: false, NeedsClarification: true);
            }

            if (ContainsAny(normalized, "создай", "измени", "исправь", "удали", "добавь"))
            {
                return new IntentDecision(UnifiedIntentKind.Execute, 0.90, "explicit_mutation_phrase", MutationAllowed: true, NeedsClarification: false);
            }

            if (scored == TaskIntentKind.Execute)
            {
                return new IntentDecision(UnifiedIntentKind.Execute, 0.80, "task_intent_execute", MutationAllowed: true, NeedsClarification: false);
            }

            if (TaskPrecheckHeuristics.IsLowSignalTask(value))
            {
                return new IntentDecision(UnifiedIntentKind.Clarify, 0.80, "low_signal", MutationAllowed: false, NeedsClarification: true);
            }

            return new IntentDecision(UnifiedIntentKind.Clarify, 0.60, "low_confidence_fallback", MutationAllowed: false, NeedsClarification: true);
        }

        private static bool ContainsAny(string text, params string[] markers)
        {
            foreach (var marker in markers)
            {
                if (text.Contains(marker, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool IsBroadVagueMutation(string text)
        {
            var hasBroadTarget = text.Contains("всё", StringComparison.Ordinal) || text.Contains("все", StringComparison.Ordinal);
            var hasMutationVerb = ContainsAny(text, "почини", "исправь", "сделай");
            return hasBroadTarget && hasMutationVerb;
        }
    }
}
