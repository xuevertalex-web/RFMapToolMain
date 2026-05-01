using LocalCursorAgent.Security;

namespace LocalCursorAgent.Core
{
    internal static class ApprovalStatusSummaryBuilder
    {
        public static Agent.ApprovalStatusSummaryPayload Build(IReadOnlyList<ActionLifecycleEntry> entries)
        {
            var allowed = 0;
            var approvalRequired = 0;
            var denied = 0;
            var notApplicable = 0;

            foreach (var entry in entries)
            {
                var status = (entry?.ApprovalStatus ?? string.Empty).Trim();
                if (status.Equals(ApprovalStatus.Allowed.ToString(), StringComparison.OrdinalIgnoreCase))
                    allowed++;
                else if (status.Equals(ApprovalStatus.ApprovalRequired.ToString(), StringComparison.OrdinalIgnoreCase))
                    approvalRequired++;
                else if (status.Equals(ApprovalStatus.Denied.ToString(), StringComparison.OrdinalIgnoreCase))
                    denied++;
                else
                    notApplicable++;
            }

            return new Agent.ApprovalStatusSummaryPayload
            {
                Allowed = allowed,
                ApprovalRequired = approvalRequired,
                Denied = denied,
                NotApplicable = notApplicable
            };
        }
    }
}
