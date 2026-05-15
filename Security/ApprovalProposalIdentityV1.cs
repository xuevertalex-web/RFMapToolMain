using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LocalCursorAgent.Security;

internal static class ApprovalProposalIdentityV1
{
    public const int IdentityVersion = 1;
    private const string Prefix = "proposal-v1:";

    public static string ComputeProposalId(
        string sessionId,
        ToolAction action,
        PermissionReasonCode reasonCode,
        string normalizedTarget,
        string normalizedWorkspace,
        CapabilityFingerprintV1? capabilityFingerprint)
    {
        var normalizedSessionId = NormalizeOptional(sessionId) ?? string.Empty;
        var normalizedTargetValue = NormalizeOptional(normalizedTarget) ?? string.Empty;
        var normalizedWorkspaceValue = NormalizeOptional(normalizedWorkspace) ?? string.Empty;
        var normalizedReasonCode = PermissionDecision.ToReasonCodeString(reasonCode);

        var hasCommandMetadata = action.Kind == ToolActionKind.RunCommand &&
                                 (!string.IsNullOrWhiteSpace(action.CommandExecutable) ||
                                  (action.CommandArgs is { Count: > 0 }));

        var normalizedPayload = action.Kind == ToolActionKind.RunCommand && hasCommandMetadata
            ? null
            : NormalizePayload(action.Payload, collapseWhitespace: action.Kind == ToolActionKind.RunCommand);
        var normalizedCommandExecutable = action.Kind == ToolActionKind.RunCommand
            ? NormalizeOptional(action.CommandExecutable)
            : null;
        var normalizedCommandArgs = action.Kind == ToolActionKind.RunCommand
            ? NormalizeCommandArgs(action.CommandArgs)
            : Array.Empty<string>();

        var canonicalBytes = BuildCanonicalPayload(
            normalizedSessionId,
            action.Kind.ToString(),
            normalizedReasonCode,
            normalizedTargetValue,
            normalizedWorkspaceValue,
            normalizedPayload,
            normalizedCommandExecutable,
            hasCommandMetadata,
            normalizedCommandArgs,
            capabilityFingerprint);
        var hash = Convert.ToHexString(SHA256.HashData(canonicalBytes)).ToLowerInvariant();
        return Prefix + hash;
    }

    private static byte[] BuildCanonicalPayload(
        string sessionId,
        string actionKind,
        string reasonCode,
        string normalizedTarget,
        string normalizedWorkspace,
        string? normalizedPayload,
        string? normalizedCommandExecutable,
        bool hasCommandMetadata,
        IReadOnlyList<string> normalizedCommandArgs,
        CapabilityFingerprintV1? capabilityFingerprint)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("identityVersion", IdentityVersion);
            writer.WriteString("sessionId", sessionId);
            writer.WriteString("actionKind", actionKind);
            writer.WriteString("reasonCode", reasonCode);
            writer.WriteString("normalizedTarget", normalizedTarget);
            writer.WriteString("normalizedWorkspace", normalizedWorkspace);
            WriteNullableString(writer, "normalizedPayload", normalizedPayload);
            WriteNullableString(writer, "normalizedCommandExecutable", normalizedCommandExecutable);
            writer.WriteBoolean("hasCommandMetadata", hasCommandMetadata);
            writer.WritePropertyName("normalizedCommandArgs");
            writer.WriteStartArray();
            foreach (var arg in normalizedCommandArgs)
                writer.WriteStringValue(arg);
            writer.WriteEndArray();
            WriteCapabilityFingerprint(writer, capabilityFingerprint);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteCapabilityFingerprint(Utf8JsonWriter writer, CapabilityFingerprintV1? capabilityFingerprint)
    {
        if (capabilityFingerprint is null)
        {
            writer.WriteNull("capabilityFingerprint");
            return;
        }

        writer.WritePropertyName("capabilityFingerprint");
        writer.WriteStartObject();
        writer.WriteNumber("fingerprintVersion", capabilityFingerprint.FingerprintVersion);
        writer.WriteString("actionKind", capabilityFingerprint.ActionKind);
        writer.WriteString("capabilityClass", capabilityFingerprint.CapabilityClass);
        writer.WriteNumber("capabilityTier", capabilityFingerprint.CapabilityTier);
        writer.WriteString("capabilityGate", capabilityFingerprint.CapabilityGate);
        WriteNullableString(writer, "policyCategory", NormalizeOptional(capabilityFingerprint.PolicyCategory));
        writer.WriteString("actionProfile", capabilityFingerprint.ActionProfile);
        writer.WriteEndObject();
    }

    private static string[] NormalizeCommandArgs(IReadOnlyList<string>? args)
    {
        if (args is null || args.Count == 0)
            return Array.Empty<string>();

        var normalized = new List<string>(args.Count);
        foreach (var arg in args)
            normalized.Add(NormalizePayload(arg, collapseWhitespace: false) ?? string.Empty);
        return normalized.ToArray();
    }

    private static string? NormalizePayload(string? value, bool collapseWhitespace)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
            return null;

        normalized = normalized.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        if (!collapseWhitespace)
            return normalized;

        var sb = new StringBuilder(normalized.Length);
        var lastWasWhitespace = false;
        foreach (var ch in normalized)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasWhitespace)
                {
                    sb.Append(' ');
                    lastWasWhitespace = true;
                }
                continue;
            }

            sb.Append(ch);
            lastWasWhitespace = false;
        }

        return NormalizeOptional(sb.ToString());
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
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
}
