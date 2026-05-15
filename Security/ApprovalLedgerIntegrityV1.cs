using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LocalCursorAgent.Security;

internal sealed record ApprovalLedgerIntegrityEventCore(
    int SchemaVersion,
    string Event,
    DateTime AtUtc,
    string SessionId,
    string ProposalId,
    string? RunId,
    DateTime? ExpiresAtUtc,
    string ReasonCode,
    string ActionType,
    CapabilityFingerprintV1? CapabilityFingerprint);

internal static class ApprovalLedgerIntegrityV1
{
    public const int Version = 1;

    public static string ComputeEventHash(ApprovalLedgerIntegrityEventCore core, string? prevEventHash)
    {
        var canonical = BuildCanonicalPayload(core, prevEventHash);
        var hash = SHA256.HashData(canonical);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool IsValidHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (value.Length != 64)
            return false;
        foreach (var ch in value)
        {
            var digit = char.IsDigit(ch);
            var lowerHex = ch is >= 'a' and <= 'f';
            if (!digit && !lowerHex)
                return false;
        }

        return true;
    }

    private static byte[] BuildCanonicalPayload(ApprovalLedgerIntegrityEventCore core, string? prevEventHash)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("integrityVersion", Version);
            WriteNullableString(writer, "prevEventHash", NormalizeHash(prevEventHash));
            writer.WriteNumber("schemaVersion", core.SchemaVersion);
            writer.WriteString("event", core.Event);
            writer.WriteString("atUtc", NormalizeUtc(core.AtUtc));
            writer.WriteString("sessionId", core.SessionId);
            writer.WriteString("proposalId", core.ProposalId);
            WriteNullableString(writer, "runId", NormalizeOptionalValue(core.RunId));
            WriteNullableUtc(writer, "expiresAtUtc", core.ExpiresAtUtc);
            writer.WriteString("reasonCode", core.ReasonCode);
            writer.WriteString("actionType", core.ActionType);
            WriteCapabilityFingerprint(writer, core.CapabilityFingerprint);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteCapabilityFingerprint(Utf8JsonWriter writer, CapabilityFingerprintV1? fingerprint)
    {
        if (fingerprint is null)
        {
            writer.WriteNull("capabilityFingerprint");
            return;
        }

        writer.WritePropertyName("capabilityFingerprint");
        writer.WriteStartObject();
        writer.WriteNumber("fingerprintVersion", fingerprint.FingerprintVersion);
        writer.WriteString("actionKind", fingerprint.ActionKind);
        writer.WriteString("capabilityClass", fingerprint.CapabilityClass);
        writer.WriteNumber("capabilityTier", fingerprint.CapabilityTier);
        writer.WriteString("capabilityGate", fingerprint.CapabilityGate);
        WriteNullableString(writer, "policyCategory", NormalizeOptionalValue(fingerprint.PolicyCategory));
        writer.WriteString("actionProfile", fingerprint.ActionProfile);
        writer.WriteEndObject();
    }

    private static void WriteNullableUtc(Utf8JsonWriter writer, string propertyName, DateTime? value)
    {
        if (!value.HasValue)
        {
            writer.WriteNull(propertyName);
            return;
        }

        writer.WriteString(propertyName, NormalizeUtc(value.Value));
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            writer.WriteNull(propertyName);
            return;
        }

        writer.WriteString(propertyName, value);
    }

    private static string NormalizeUtc(DateTime value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string? NormalizeOptionalValue(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeHash(string? value)
    {
        var normalized = NormalizeOptionalValue(value);
        if (normalized is null)
            return null;
        return normalized.ToLowerInvariant();
    }
}
