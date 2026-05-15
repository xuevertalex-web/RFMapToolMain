using System.Text;
using System.Text.Json;

namespace LocalCursorAgent.Security;

internal sealed class ApprovalLedgerV2
{
    private const int SchemaVersion = 2;
    private const int StartupCompactionLineThreshold = 400;
    private const long StartupCompactionSizeThresholdBytes = 256 * 1024;
    private const int RetainedDeniedEventsPerProposal = 10;
    private readonly string _ledgerPath;

    public ApprovalLedgerV2(string runtimeRoot)
    {
        _ledgerPath = Path.Combine(runtimeRoot, "approval-ledger-v2.jsonl");
    }

    public string LedgerPath => _ledgerPath;
    public string CompactTempPath => _ledgerPath + ".compact.tmp";
    public string CompactBackupPath => _ledgerPath + ".compact.bak";

    public bool TryLoad(out Dictionary<string, ActionApprovalProposal> proposals, out HashSet<string> consumed, out HashSet<string> expired, out string? error)
    {
        proposals = new Dictionary<string, ActionApprovalProposal>(StringComparer.OrdinalIgnoreCase);
        consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        expired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                var runId = root.TryGetProperty("runId", out var runIdProp) ? (runIdProp.GetString() ?? string.Empty) : string.Empty;
                if (!TryReadCapabilityFingerprint(root, out var capabilityFingerprint, out error))
                    return false;
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
                        ActionType = actionType,
                        RunId = runId,
                        CapabilityFingerprint = capabilityFingerprint
                    };
                    continue;
                }

                if (evt.Equals("consumed", StringComparison.OrdinalIgnoreCase))
                {
                    consumed.Add(proposalId);
                    continue;
                }

                if (evt.Equals("expired", StringComparison.OrdinalIgnoreCase))
                {
                    if (!consumed.Contains(proposalId))
                        expired.Add(proposalId);
                    continue;
                }

                if (evt.StartsWith("denied_", StringComparison.OrdinalIgnoreCase))
                {
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

    public bool TryCompactForStartup(DateTime nowUtc, int approvalTtlSeconds, out bool compacted, out string? error)
    {
        compacted = false;
        error = null;
        try
        {
            if (!File.Exists(_ledgerPath))
                return true;

            var fileInfo = new FileInfo(_ledgerPath);
            if (fileInfo.Length < StartupCompactionSizeThresholdBytes)
            {
                var lines = CountLines(_ledgerPath);
                if (lines < StartupCompactionLineThreshold)
                    return true;
            }

            if (!TryParseLedger(out var records, out error))
                return false;

            var keepWindowSeconds = Math.Max(approvalTtlSeconds * 3, 24 * 60 * 60);
            var keepWindowStart = nowUtc.AddSeconds(-keepWindowSeconds);
            var compactedRecords = BuildCompactedRecords(records, nowUtc, keepWindowStart);
            var compactedJson = compactedRecords.Select(ToJsonLine).ToArray();

            WriteValidatedTemp(compactedJson);
            if (!TryParseLedgerFile(CompactTempPath, out _, out error))
                return false;

            ReplaceAtomically();
            compacted = true;
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
        if (proposal.IssuedAtUtc >= AgentSessionContext.RunIdCutoverUtc && string.IsNullOrWhiteSpace(proposal.RunId))
        {
            error = "Approval runId is required for post-cutover issued records.";
            return false;
        }

        var record = new
        {
            schemaVersion = SchemaVersion,
            @event = "issued",
            atUtc = proposal.IssuedAtUtc,
            sessionId = proposal.SessionId,
            proposalId = proposal.ProposalId,
            runId = NormalizeOptionalRunId(proposal.RunId),
            expiresAtUtc = proposal.ExpiresAtUtc,
            reasonCode = proposal.ReasonCode,
            actionType = proposal.ActionType,
            capabilityFingerprint = CreateCapabilityFingerprintPayload(proposal.CapabilityFingerprint)
        };
        return TryAppendRecord(record, out error);
    }

    public bool TryAppendConsumed(string sessionId, string proposalId, DateTime atUtc, string? runId, CapabilityFingerprintV1? capabilityFingerprint, out string? error)
    {
        var record = new
        {
            schemaVersion = SchemaVersion,
            @event = "consumed",
            atUtc,
            sessionId,
            proposalId,
            runId = NormalizeOptionalRunId(runId),
            capabilityFingerprint = CreateCapabilityFingerprintPayload(capabilityFingerprint)
        };
        return TryAppendRecord(record, out error);
    }

    public bool TryAppendExpired(string sessionId, string proposalId, DateTime atUtc, string reasonCode, string? runId, CapabilityFingerprintV1? capabilityFingerprint, out string? error)
    {
        var record = new
        {
            schemaVersion = SchemaVersion,
            @event = "expired",
            atUtc,
            sessionId,
            proposalId,
            reasonCode,
            runId = NormalizeOptionalRunId(runId),
            capabilityFingerprint = CreateCapabilityFingerprintPayload(capabilityFingerprint)
        };
        return TryAppendRecord(record, out error);
    }

    public bool TryAppendDenied(string sessionId, string proposalId, string eventName, DateTime atUtc, string reasonCode, string? runId, out string? error)
    {
        var record = new
        {
            schemaVersion = SchemaVersion,
            @event = eventName,
            atUtc,
            sessionId,
            proposalId,
            reasonCode,
            runId = NormalizeOptionalRunId(runId)
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

    private static int CountLines(string path)
    {
        var count = 0;
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        while (reader.ReadLine() is not null)
            count++;
        return count;
    }

    private bool TryParseLedger(out List<LedgerRecord> records, out string? error) =>
        TryParseLedgerFile(_ledgerPath, out records, out error);

    private bool TryParseLedgerFile(string path, out List<LedgerRecord> records, out string? error)
    {
        records = new List<LedgerRecord>();
        error = null;
        try
        {
            if (!File.Exists(path))
                return true;
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
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
                var sessionId = root.GetProperty("sessionId").GetString() ?? string.Empty;
                var proposalId = root.GetProperty("proposalId").GetString() ?? string.Empty;
                var atUtc = root.GetProperty("atUtc").GetDateTime();
                if (string.IsNullOrWhiteSpace(proposalId))
                {
                    error = "Approval ledger record is missing proposalId.";
                    return false;
                }

                DateTime? expiresAtUtc = null;
                if (root.TryGetProperty("expiresAtUtc", out var expiresProp))
                    expiresAtUtc = expiresProp.GetDateTime();
                var reasonCode = root.TryGetProperty("reasonCode", out var reason) ? reason.GetString() ?? string.Empty : string.Empty;
                var actionType = root.TryGetProperty("actionType", out var action) ? action.GetString() ?? string.Empty : string.Empty;
                var runId = root.TryGetProperty("runId", out var runIdProp) ? runIdProp.GetString() : null;
                if (!TryReadCapabilityFingerprint(root, out var capabilityFingerprint, out error))
                    return false;
                records.Add(new LedgerRecord(evt, atUtc, sessionId, proposalId, expiresAtUtc, reasonCode, actionType, runId, capabilityFingerprint));
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static List<LedgerRecord> BuildCompactedRecords(List<LedgerRecord> source, DateTime nowUtc, DateTime keepWindowStartUtc)
    {
        var groups = source
            .OrderBy(x => x.AtUtc)
            .GroupBy(x => x.ProposalId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var output = new List<LedgerRecord>();

        foreach (var group in groups)
        {
            var ordered = group.ToList();
            var issued = ordered.LastOrDefault(x => x.Event.Equals("issued", StringComparison.OrdinalIgnoreCase));
            var hasConsumed = ordered.Any(x => x.Event.Equals("consumed", StringComparison.OrdinalIgnoreCase));
            var expiredRecords = ordered.Where(x => x.Event.Equals("expired", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.AtUtc).ToList();
            var terminal = hasConsumed ? "consumed" : (expiredRecords.Count > 0 ? "expired" : "none");
            var terminalAt = hasConsumed
                ? ordered.Where(x => x.Event.Equals("consumed", StringComparison.OrdinalIgnoreCase)).Max(x => x.AtUtc)
                : (expiredRecords.Count > 0 ? expiredRecords.Last().AtUtc : DateTime.MinValue);
            var hasTerminal = terminal != "none";
            var retainTerminal = hasTerminal && terminalAt >= keepWindowStartUtc;
            var issuedActive = issued is not null && !hasTerminal && (!issued.ExpiresAtUtc.HasValue || nowUtc <= issued.ExpiresAtUtc.Value);

            if (issuedActive)
                output.Add(issued!);
            if (retainTerminal)
            {
                if (terminal == "consumed")
                {
                    var consumed = ordered.Where(x => x.Event.Equals("consumed", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.AtUtc).Last();
                    output.Add(consumed);
                }
                else
                {
                    output.Add(expiredRecords.Last());
                }
            }

            var retainDiagnostics = issuedActive || retainTerminal;
            if (!retainDiagnostics)
                continue;
            var denied = ordered.Where(x => x.Event.StartsWith("denied_", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.AtUtc)
                .TakeLast(RetainedDeniedEventsPerProposal)
                .ToList();
            output.AddRange(denied);
        }

        return output
            .OrderBy(x => x.ProposalId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => EventOrder(x.Event))
            .ThenBy(x => x.AtUtc)
            .ToList();
    }

    private static int EventOrder(string evt) => evt switch
    {
        "issued" => 0,
        "consumed" => 1,
        "expired" => 2,
        _ when evt.StartsWith("denied_", StringComparison.OrdinalIgnoreCase) => 3,
        _ => 9
    };

    private static string ToJsonLine(LedgerRecord record)
    {
        var fingerprintPayload = CreateCapabilityFingerprintPayload(record.CapabilityFingerprint);
        object payload = record.Event switch
        {
            "issued" => new
            {
                schemaVersion = SchemaVersion,
                @event = record.Event,
                atUtc = record.AtUtc,
                sessionId = record.SessionId,
                proposalId = record.ProposalId,
                runId = NormalizeOptionalRunId(record.RunId),
                expiresAtUtc = record.ExpiresAtUtc,
                reasonCode = record.ReasonCode,
                actionType = record.ActionType,
                capabilityFingerprint = fingerprintPayload
            },
            "consumed" => new
            {
                schemaVersion = SchemaVersion,
                @event = record.Event,
                atUtc = record.AtUtc,
                sessionId = record.SessionId,
                proposalId = record.ProposalId,
                runId = NormalizeOptionalRunId(record.RunId),
                capabilityFingerprint = fingerprintPayload
            },
            "expired" => new
            {
                schemaVersion = SchemaVersion,
                @event = record.Event,
                atUtc = record.AtUtc,
                sessionId = record.SessionId,
                proposalId = record.ProposalId,
                reasonCode = record.ReasonCode,
                runId = NormalizeOptionalRunId(record.RunId),
                capabilityFingerprint = fingerprintPayload
            },
            _ => new
            {
                schemaVersion = SchemaVersion,
                @event = record.Event,
                atUtc = record.AtUtc,
                sessionId = record.SessionId,
                proposalId = record.ProposalId,
                reasonCode = record.ReasonCode,
                runId = NormalizeOptionalRunId(record.RunId)
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private void WriteValidatedTemp(IEnumerable<string> lines)
    {
        var dir = Path.GetDirectoryName(CompactTempPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        using var stream = new FileStream(CompactTempPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        foreach (var line in lines)
            writer.WriteLine(line);
        writer.Flush();
        stream.Flush(true);
    }

    private void ReplaceAtomically()
    {
        if (File.Exists(CompactBackupPath))
            File.Delete(CompactBackupPath);
        File.Replace(CompactTempPath, _ledgerPath, CompactBackupPath, ignoreMetadataErrors: true);
        if (File.Exists(CompactBackupPath))
            File.Delete(CompactBackupPath);
    }

    private sealed record LedgerRecord(
        string Event,
        DateTime AtUtc,
        string SessionId,
        string ProposalId,
        DateTime? ExpiresAtUtc,
        string ReasonCode,
        string ActionType,
        string? RunId,
        CapabilityFingerprintV1? CapabilityFingerprint);

    private static object? CreateCapabilityFingerprintPayload(CapabilityFingerprintV1? fingerprint)
    {
        if (fingerprint is null)
            return null;

        return new
        {
            fingerprintVersion = fingerprint.FingerprintVersion,
            actionKind = fingerprint.ActionKind,
            capabilityClass = fingerprint.CapabilityClass,
            capabilityTier = fingerprint.CapabilityTier,
            capabilityGate = fingerprint.CapabilityGate,
            policyCategory = NormalizeOptionalValue(fingerprint.PolicyCategory),
            actionProfile = fingerprint.ActionProfile
        };
    }

    private static bool TryReadCapabilityFingerprint(JsonElement root, out CapabilityFingerprintV1? fingerprint, out string? error)
    {
        fingerprint = null;
        error = null;
        if (!root.TryGetProperty("capabilityFingerprint", out var fingerprintProp))
            return true;
        if (fingerprintProp.ValueKind == JsonValueKind.Null)
            return true;
        if (fingerprintProp.ValueKind != JsonValueKind.Object)
        {
            error = "Approval ledger capabilityFingerprint must be an object.";
            return false;
        }

        if (!fingerprintProp.TryGetProperty("fingerprintVersion", out var versionProp) || versionProp.ValueKind != JsonValueKind.Number || versionProp.GetInt32() != CapabilityFingerprintV1.CurrentVersion)
        {
            error = "Approval ledger capabilityFingerprint has unsupported fingerprintVersion.";
            return false;
        }

        if (!TryReadRequiredString(fingerprintProp, "actionKind", out var actionKind, out error))
            return false;
        if (!Enum.TryParse<ToolActionKind>(actionKind, ignoreCase: false, out _))
        {
            error = "Approval ledger capabilityFingerprint actionKind is invalid.";
            return false;
        }

        if (!TryReadRequiredString(fingerprintProp, "capabilityClass", out var capabilityClass, out error))
            return false;
        if (!TryReadRequiredInt(fingerprintProp, "capabilityTier", out var capabilityTier, out error))
            return false;
        if (!TryReadRequiredString(fingerprintProp, "capabilityGate", out var capabilityGate, out error))
            return false;
        if (!TryReadRequiredString(fingerprintProp, "actionProfile", out var actionProfile, out error))
            return false;
        if (!CapabilityFingerprintV1.IsKnownActionProfile(actionProfile))
        {
            error = "Approval ledger capabilityFingerprint actionProfile is invalid.";
            return false;
        }

        string? policyCategory = null;
        if (fingerprintProp.TryGetProperty("policyCategory", out var policyCategoryProp))
        {
            if (policyCategoryProp.ValueKind == JsonValueKind.String)
                policyCategory = NormalizeOptionalValue(policyCategoryProp.GetString());
            else if (policyCategoryProp.ValueKind != JsonValueKind.Null)
            {
                error = "Approval ledger capabilityFingerprint policyCategory must be a string or null.";
                return false;
            }
        }

        fingerprint = new CapabilityFingerprintV1
        {
            FingerprintVersion = CapabilityFingerprintV1.CurrentVersion,
            ActionKind = actionKind,
            CapabilityClass = capabilityClass,
            CapabilityTier = capabilityTier,
            CapabilityGate = capabilityGate,
            PolicyCategory = policyCategory,
            ActionProfile = actionProfile
        };
        return true;
    }

    private static bool TryReadRequiredString(JsonElement obj, string propertyName, out string value, out string? error)
    {
        value = string.Empty;
        error = null;
        if (!obj.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            error = $"Approval ledger capabilityFingerprint is missing {propertyName}.";
            return false;
        }

        value = property.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"Approval ledger capabilityFingerprint {propertyName} is empty.";
            return false;
        }

        return true;
    }

    private static bool TryReadRequiredInt(JsonElement obj, string propertyName, out int value, out string? error)
    {
        value = default;
        error = null;
        if (!obj.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            error = $"Approval ledger capabilityFingerprint is missing {propertyName}.";
            return false;
        }

        value = property.GetInt32();
        return true;
    }

    private static string? NormalizeOptionalRunId(string? runId)
    {
        var normalized = runId?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
