using LocalCursorAgent.Security;

using LocalCursorAgent.Diagnostics;

namespace LocalCursorAgent.Core
{
    internal static class ApprovalStatusSummaryBuilder
    {
        private const string ApprovalStatusAllowed = "Allowed";
        private const string ApprovalStatusApprovalRequired = "ApprovalRequired";
        private const string ApprovalStatusDenied = "Denied";

        public static Agent.ApprovalStatusSummaryPayload Build(IReadOnlyList<ActionLifecycleEntry> entries)
        {
            var allowed = 0;
            var approvalRequired = 0;
            var denied = 0;
            var notApplicable = 0;

            foreach (var entry in entries)
            {
                var status = (entry?.ApprovalStatus ?? string.Empty).Trim();
                if (status.Equals(ApprovalStatusAllowed, StringComparison.OrdinalIgnoreCase))
                    allowed++;
                else if (status.Equals(ApprovalStatusApprovalRequired, StringComparison.OrdinalIgnoreCase))
                    approvalRequired++;
                else if (status.Equals(ApprovalStatusDenied, StringComparison.OrdinalIgnoreCase))
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
