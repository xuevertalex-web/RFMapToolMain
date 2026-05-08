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
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        return payload.Contains("APPROVED:true", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCommandPayload(string? payload)
    {
        var text = payload?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return string.Empty;

        var marker = "APPROVED:true";
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            text = text.Remove(idx, marker.Length).Trim();

        return text;
    }
}
