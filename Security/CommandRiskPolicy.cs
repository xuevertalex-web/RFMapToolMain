namespace LocalCursorAgent.Security;

internal static class CommandRiskPolicy
{
    public static bool IsHighRiskCommand(string? payload)
    {
        var text = NormalizeCommandPayload(payload);
        if (text.Length == 0)
            return false;

        var lowered = text.ToLowerInvariant();
        return lowered.Contains("nvidia-smi", StringComparison.Ordinal) ||
               lowered.Contains("curl ", StringComparison.Ordinal) ||
               lowered.Contains("wget ", StringComparison.Ordinal) ||
               lowered.Contains("invoke-webrequest", StringComparison.Ordinal) ||
               lowered.Contains("invoke-restmethod", StringComparison.Ordinal) ||
               lowered.Contains("netstat", StringComparison.Ordinal) ||
               lowered.Contains("tcpdump", StringComparison.Ordinal) ||
               lowered.Contains("wireshark", StringComparison.Ordinal) ||
               lowered.Contains("tasklist", StringComparison.Ordinal) ||
               lowered.Contains("get-process", StringComparison.Ordinal) ||
               lowered.Contains("wmic", StringComparison.Ordinal) ||
               lowered.Contains("pip install", StringComparison.Ordinal) ||
               lowered.Contains("npm install -g", StringComparison.Ordinal) ||
               lowered.Contains("winget install", StringComparison.Ordinal) ||
               lowered.Contains("choco install", StringComparison.Ordinal) ||
               lowered.Contains("apt install", StringComparison.Ordinal) ||
               lowered.Contains("yum install", StringComparison.Ordinal) ||
               lowered.Contains("dnf install", StringComparison.Ordinal) ||
               lowered.Contains("powershell -enc", StringComparison.Ordinal) ||
               lowered.Contains("remove-item -recurse", StringComparison.Ordinal) ||
               lowered.Contains("rm -rf", StringComparison.Ordinal);
    }

    public static string ResolveCommandRiskLevel(string? payload)
    {
        var lowered = NormalizeCommandPayload(payload).ToLowerInvariant();
        if (lowered.Contains("rm -rf", StringComparison.Ordinal) ||
            lowered.Contains("remove-item -recurse", StringComparison.Ordinal) ||
            lowered.Contains("powershell -enc", StringComparison.Ordinal))
        {
            return "high";
        }

        return "medium";
    }

    public static bool HasExplicitApprovalMarker(string? payload)
    {
        return TryExtractApprovalToken(payload, out _);
    }

    public static bool TryExtractApprovalToken(string? payload, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        const string marker = "APPROVED:";
        var idx = payload.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return false;

        var rest = payload[(idx + marker.Length)..].Trim();
        if (string.IsNullOrWhiteSpace(rest))
            return false;

        var tokenPart = rest.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tokenPart))
            return false;

        token = tokenPart.Trim();
        return token.Length > 0;
    }

    private static string NormalizeCommandPayload(string? payload)
    {
        var text = payload?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return string.Empty;

        var marker = "APPROVED:";
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var tokenEnd = text.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }, idx + marker.Length);
            text = tokenEnd >= 0
                ? (text.Remove(idx, tokenEnd - idx)).Trim()
                : text[..idx].Trim();
        }

        return text;
    }
}
