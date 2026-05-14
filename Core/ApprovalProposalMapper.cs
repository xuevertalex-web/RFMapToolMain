using LocalCursorAgent.Security;

namespace LocalCursorAgent.Core
{
    internal static class ApprovalProposalMapper
    {
        public static Agent.ApprovalRequiredActionPayload[] MapApprovalProposals(IReadOnlyList<ActionApprovalProposal> proposals)
        {
            return proposals.Select(p => new Agent.ApprovalRequiredActionPayload
            {
                ProposalId = p.ProposalId,
                ApprovalTokenFormat = string.IsNullOrWhiteSpace(p.ProposalId) ? string.Empty : $"APPROVED:{p.ProposalId}",
                ActionType = p.ActionType,
                Command = p.Command ?? string.Empty,
                Path = p.Path ?? string.Empty,
                NormalizedTarget = p.NormalizedTarget ?? string.Empty,
                SandboxRoot = p.SandboxRoot,
                ProjectRoot = p.ProjectRoot,
                WorktreeRoot = p.WorktreeRoot,
                RiskLevel = p.RiskLevel,
                ReasonCode = p.ReasonCode,
                ExpectedEffect = p.ExpectedEffect,
                Reason = p.Reason,
                ApprovalStatus = p.ApprovalStatus.ToString(),
                IsInsideSandbox = p.IsInsideSandbox,
                IssuedAtUtc = p.IssuedAtUtc == default ? string.Empty : p.IssuedAtUtc.ToUniversalTime().ToString("O"),
                ExpiresAtUtc = p.ExpiresAtUtc == default ? string.Empty : p.ExpiresAtUtc.ToUniversalTime().ToString("O"),
                TtlSeconds = p.TtlSeconds,
                SessionBound = p.SessionBound
            }).ToArray();
        }
    }
}
