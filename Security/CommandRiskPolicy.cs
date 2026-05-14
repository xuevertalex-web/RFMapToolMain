namespace LocalCursorAgent.Security;

internal static class CommandRiskPolicy
{
    public static bool IsHighRiskCommand(string? payload)
    {
        var text = NormalizeCommandPayload(payload);
        if (text.Length == 0)
            return false;

        var lowered = text.ToLowerInvariant();
        if (ContainsShellMeta(lowered))
            return true;

        var tokens = Tokenize(lowered);
        if (tokens.Count == 0)
            return false;

        return ContainsHighRiskPattern(tokens);
    }

    public static string ResolveCommandRiskLevel(string? payload)
    {
        var text = NormalizeCommandPayload(payload);
        if (text.Length == 0)
            return "medium";

        var lowered = text.ToLowerInvariant();
        if (ContainsCriticalPattern(Tokenize(lowered)) || ContainsShellMeta(lowered))
        {
            return "high";
        }

        return "medium";
    }

    private static bool ContainsHighRiskPattern(IReadOnlyList<string> tokens)
    {
        if (ContainsCriticalPattern(tokens))
            return true;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token is "curl" or "wget" or "invoke-webrequest" or "invoke-restmethod" or "netstat" or "tcpdump" or "wireshark" or "tasklist" or "get-process" or "wmic" or "nvidia-smi")
                return true;

            if ((token is "pip" or "winget" or "choco" or "apt" or "yum" or "dnf") && i + 1 < tokens.Count && tokens[i + 1] == "install")
                return true;

            if (token == "npm" && i + 2 < tokens.Count && tokens[i + 1] == "install" && tokens[i + 2] == "-g")
                return true;
        }

        return false;
    }

    private static bool ContainsCriticalPattern(IReadOnlyList<string> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token is "-encodedcommand" or "-enc" or "-rf" or "--force" or "-force" or "/s" or "/q")
                return true;
            if (token == "npm" && i + 1 < tokens.Count && tokens[i + 1] == "run")
                return true;
            if (token == "npx")
                return true;
            if (token is "iex" or "invoke-expression" or "start-process")
                return true;
            if (token is "rm" or "del" or "erase" or "rmdir" or "rd" or "remove-item" or "ri")
                return true;
            if (token == "git" && MatchesGitDestructive(tokens, i))
                return true;
        }

        return false;
    }

    private static bool MatchesGitDestructive(IReadOnlyList<string> tokens, int gitIndex)
    {
        if (gitIndex + 2 < tokens.Count && tokens[gitIndex + 1] == "reset" && tokens[gitIndex + 2] == "--hard")
            return true;
        if (gitIndex + 2 < tokens.Count && tokens[gitIndex + 1] == "clean" && IsDestructiveGitCleanFlags(tokens[gitIndex + 2]))
            return true;
        if (gitIndex + 2 < tokens.Count && tokens[gitIndex + 1] == "checkout" && tokens[gitIndex + 2] == ".")
            return true;
        if (gitIndex + 3 < tokens.Count && tokens[gitIndex + 1] == "checkout" && tokens[gitIndex + 2] == "--" && tokens[gitIndex + 3] == ".")
            return true;
        if (gitIndex + 2 < tokens.Count && tokens[gitIndex + 1] == "restore" && tokens[gitIndex + 2] == ".")
            return true;
        if (gitIndex + 2 < tokens.Count && tokens[gitIndex + 1] == "restore" && IsDestructiveGitRestoreFlags(tokens, gitIndex + 2))
            return true;
        return false;
    }

    private static bool IsDestructiveGitCleanFlags(string flags)
    {
        if (string.IsNullOrWhiteSpace(flags))
            return false;

        var compact = flags.Trim().TrimStart('-').Replace("-", string.Empty, StringComparison.Ordinal);
        return compact.Contains("f", StringComparison.Ordinal) && compact.Contains("d", StringComparison.Ordinal);
    }

    private static bool IsDestructiveGitRestoreFlags(IReadOnlyList<string> tokens, int flagsStartIndex)
    {
        var hasDotTarget = false;
        var hasWorktree = false;
        var hasStaged = false;

        for (var i = flagsStartIndex; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token == ".")
            {
                hasDotTarget = true;
                break;
            }

            if (token == "--worktree")
                hasWorktree = true;
            else if (token == "--staged")
                hasStaged = true;
            else if (!token.StartsWith("-", StringComparison.Ordinal))
                break;
        }

        return hasDotTarget && (hasWorktree || hasStaged);
    }

    private static bool ContainsShellMeta(string lowered)
    {
        return lowered.Contains("&&", StringComparison.Ordinal) ||
               lowered.Contains("||", StringComparison.Ordinal) ||
               lowered.Contains(';', StringComparison.Ordinal) ||
               lowered.Contains('|', StringComparison.Ordinal) ||
               lowered.Contains('`', StringComparison.Ordinal) ||
               lowered.Contains("$(", StringComparison.Ordinal) ||
               lowered.Contains(">>", StringComparison.Ordinal) ||
               lowered.Contains('>', StringComparison.Ordinal);
    }

    private static List<string> Tokenize(string text)
    {
        var split = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var tokens = new List<string>(split.Length);
        foreach (var raw in split)
        {
            var token = raw.Trim().Trim('"', '\'');
            if (token.Length == 0)
                continue;

            tokens.Add(token);
        }

        return tokens;
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
