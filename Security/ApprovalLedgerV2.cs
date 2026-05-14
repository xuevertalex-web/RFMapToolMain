using System.Text;
using System.Text.Json;

namespace LocalCursorAgent.Security;

internal sealed class ApprovalLedgerV2
{
    private const int SchemaVersion = 2;
    private readonly string _ledgerPath;

    public ApprovalLedgerV2(string runtimeRoot)
    {
        _ledgerPath = Path.Combine(runtimeRoot, "approval-ledger-v2.jsonl");
    }

    public string LedgerPath => _ledgerPath;

    public bool TryLoad(out Dictionary<string, ActionApprovalProposal> proposals, out HashSet<string> consumed, out string? error)
    {
        proposals = new Dictionary<string, ActionApprovalProposal>(StringComparer.OrdinalIgnoreCase);
        consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        error = null;

        try
        {
            if (!File.Exists(_ledgerPath))
                return true;

            foreach (var line in File.ReadLines(_ledgerPath, Encoding.UTF8))
            {
                var text = line.Trim();
                if (text.Length == 0)
                    continue;

                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (!root.TryGetProperty("schemaVersion", out var schema) || schema.GetInt32() != SchemaVersion)
                {
                    error = "Unsupported approval ledger schema version.";
                    return false;
                }

                var evt = root.GetProperty("event").GetString() ?? string.Empty;
                var proposalId = root.GetProperty("proposalId").GetString() ?? string.Empty;
                var sessionId = root.GetProperty("sessionId").GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(proposalId))
                {
                    error = "Approval ledger record is missing proposalId.";
                    return false;
                }

                if (evt.Equals("issued", StringComparison.OrdinalIgnoreCase))
                {
                    var expiresAtUtc = root.GetProperty("expiresAtUtc").GetDateTime();
                    var reasonCode = root.TryGetProperty("reasonCode", out var reason) ? reason.GetString() ?? string.Empty : string.Empty;
                    var actionType = root.TryGetProperty("actionType", out var action) ? action.GetString() ?? string.Empty : string.Empty;
                    proposals[proposalId] = new ActionApprovalProposal
                    {
                        ProposalId = proposalId,
                        SessionId = sessionId,
                        ExpiresAtUtc = expiresAtUtc,
                        IssuedAtUtc = root.GetProperty("atUtc").GetDateTime(),
                        TtlSeconds = Math.Max(1, (int)(expiresAtUtc - root.GetProperty("atUtc").GetDateTime()).TotalSeconds),
                        RequiresApproval = true,
                        ApprovalStatus = ApprovalStatus.ApprovalRequired,
                        ReasonCode = reasonCode,
                        ActionType = actionType
                    };
                    continue;
                }

                if (evt.Equals("consumed", StringComparison.OrdinalIgnoreCase))
                {
                    consumed.Add(proposalId);
                    continue;
                }

                error = $"Unsupported approval ledger event: {evt}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryAppendIssued(ActionApprovalProposal proposal, out string? error)
    {
        var record = new
        {
            schemaVersion = SchemaVersion,
            @event = "issued",
            atUtc = proposal.IssuedAtUtc,
            sessionId = proposal.SessionId,
            proposalId = proposal.ProposalId,
            expiresAtUtc = proposal.ExpiresAtUtc,
            reasonCode = proposal.ReasonCode,
            actionType = proposal.ActionType
        };
        return TryAppendRecord(record, out error);
    }

    public bool TryAppendConsumed(string sessionId, string proposalId, DateTime atUtc, out string? error)
    {
        var record = new
        {
            schemaVersion = SchemaVersion,
            @event = "consumed",
            atUtc,
            sessionId,
            proposalId
        };
        return TryAppendRecord(record, out error);
    }

    private bool TryAppendRecord(object record, out string? error)
    {
        try
        {
            var dir = Path.GetDirectoryName(_ledgerPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(record);
            File.AppendAllText(_ledgerPath, json + Environment.NewLine, Encoding.UTF8);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
