using System;

namespace LocalCursorAgent.Memory
{
    internal static class MemoryRecordProvenance
    {
        private const string DefaultSource = MemoryGovernanceDefaults.DefaultMemorySource;
        private const string DefaultProjectScope = MemoryGovernanceDefaults.DefaultProjectScope;

        public static void Ensure(FailureRecord record)
        {
            record.Source ??= DefaultSource;
            record.ProjectScope ??= DefaultProjectScope;
        }

        public static void Ensure(SuccessRecord record)
        {
            record.Source ??= DefaultSource;
            record.ProjectScope ??= DefaultProjectScope;
        }
    }
}
