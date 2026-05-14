using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LocalCursorAgent.Security;

internal static class CommandRiskPolicy
{
    private const string ReasonAllowed = "COMMAND_ALLOWED";
    private const string ReasonHighRiskApprovalRequired = "HIGH_RISK_APPROVAL_REQUIRED";
    private const string ReasonHardBlockedCommand = "HARD_BLOCKED_COMMAND";
    private const string ReasonInvalidCommand = "INVALID_COMMAND";
    private const string ReasonUnsupportedShellMetaSyntax = "UNSUPPORTED_SHELL_META_SYNTAX";

    public static CommandPolicyDecision Evaluate(CommandPolicyInput input)
    {
        var normalized = NormalizeInput(input);
        if (normalized.Tokens.Count == 0)
        {
            return CreateDecision(
                CommandPolicyCategory.InvalidMalformed,
                ReasonInvalidCommand,
                "Command is missing executable and arguments.",
                "medium",
                approvalRequired: false,
                hardBlocked: false,
                normalized.Executable,
                normalized.Args);
        }

        if (normalized.ContainsShellMetaSyntax)
        {
            return CreateDecision(
                CommandPolicyCategory.UnsupportedShellMetaSyntax,
                ReasonUnsupportedShellMetaSyntax,
                "Shell/meta syntax is not supported in canonical command execution.",
                "high",
                approvalRequired: false,
                hardBlocked: true,
                normalized.Executable,
                normalized.Args);
        }

        if (ContainsHighRiskPattern(normalized.Tokens))
        {
            return CreateDecision(
                CommandPolicyCategory.HighRiskApprovalRequired,
                ReasonHighRiskApprovalRequired,
                "Command requires explicit approval due to high-risk side effects.",
                "high",
                approvalRequired: true,
                hardBlocked: false,
                normalized.Executable,
                normalized.Args);
        }

        return CreateDecision(
            CommandPolicyCategory.Allowed,
            ReasonAllowed,
            "Command is allowed by canonical policy.",
            "medium",
            approvalRequired: false,
            hardBlocked: false,
            normalized.Executable,
            normalized.Args);
    }

    public static bool IsHighRiskCommand(string? payload)
    {
        var decision = Evaluate(new CommandPolicyInput
        {
            RawCommandText = payload,
            Source = "legacy_payload"
        });

        return decision.Category == CommandPolicyCategory.HighRiskApprovalRequired ||
               decision.Category == CommandPolicyCategory.HardBlocked ||
               decision.Category == CommandPolicyCategory.UnsupportedShellMetaSyntax;
    }

    public static string ResolveCommandRiskLevel(string? payload)
    {
        return Evaluate(new CommandPolicyInput
        {
            RawCommandText = payload,
            Source = "legacy_payload"
        }).RiskLevel;
    }

    private static CommandPolicyDecision CreateDecision(
        string category,
        string reasonCode,
        string reasonMessage,
        string riskLevel,
        bool approvalRequired,
        bool hardBlocked,
        string normalizedExecutable,
        IReadOnlyList<string> normalizedArgs)
    {
        return new CommandPolicyDecision
        {
            Category = category,
            ReasonCode = reasonCode,
            ReasonMessage = reasonMessage,
            RiskLevel = riskLevel,
            ApprovalRequired = approvalRequired,
            HardBlocked = hardBlocked,
            NormalizedExecutable = normalizedExecutable,
            NormalizedArgs = normalizedArgs
        };
    }

    private static NormalizedIntent NormalizeInput(CommandPolicyInput input)
    {
        var normalizedExecutable = NormalizeExecutable(input.Executable);
        var normalizedArgs = NormalizeArgs(input.Args);
        var normalizedRaw = NormalizeCommandPayload(input.RawCommandText);
        var loweredRaw = normalizedRaw.ToLowerInvariant();

        var tokens = new List<string>();
        if (normalizedExecutable.Length > 0)
        {
            tokens.Add(normalizedExecutable);
        }

        foreach (var arg in normalizedArgs)
        {
            tokens.Add(arg);
        }

        if (tokens.Count == 0 && loweredRaw.Length > 0)
        {
            tokens.AddRange(Tokenize(loweredRaw));
        }

        var containsShellMeta = ContainsShellMeta(loweredRaw) || TokensContainShellMeta(tokens);
        return new NormalizedIntent(normalizedExecutable, normalizedArgs, tokens, containsShellMeta);
    }

    private static string NormalizeExecutable(string? executable)
    {
        var value = NormalizeToken(executable);
        if (value.Length == 0)
            return string.Empty;

        if (value.Contains('\\', StringComparison.Ordinal) || value.Contains('/', StringComparison.Ordinal))
        {
            value = Path.GetFileName(value);
        }

        if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".com", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            value = Path.GetFileNameWithoutExtension(value);
        }

        value = value.ToLowerInvariant();
        return value switch
        {
            "pwsh" => "powershell",
            "powershell_ise" => "powershell",
            _ => value
        };
    }

    private static IReadOnlyList<string> NormalizeArgs(IReadOnlyList<string>? args)
    {
        if (args is null || args.Count == 0)
            return Array.Empty<string>();

        var normalized = new List<string>(args.Count);
        foreach (var rawArg in args)
        {
            var token = NormalizeToken(rawArg);
            if (token.Length == 0)
                continue;

            normalized.Add(token.ToLowerInvariant());
        }

        return normalized;
    }

    private static string NormalizeToken(string? token)
    {
        return token?.Trim().Trim('"', '\'') ?? string.Empty;
    }

    private static bool TokensContainShellMeta(IReadOnlyList<string> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (TokenContainsShellMeta(tokens[i]))
                return true;
        }

        return false;
    }

    private static bool TokenContainsShellMeta(string token)
    {
        return token.Contains("&&", StringComparison.Ordinal) ||
               token.Contains("||", StringComparison.Ordinal) ||
               token.Contains(';', StringComparison.Ordinal) ||
               token.Contains('|', StringComparison.Ordinal) ||
               token.Contains('>', StringComparison.Ordinal) ||
               token.Contains('<', StringComparison.Ordinal) ||
               token.Contains('`', StringComparison.Ordinal) ||
               token.Contains("$(", StringComparison.Ordinal);
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
               lowered.Contains(">>", StringComparison.Ordinal) ||
               lowered.Contains('>', StringComparison.Ordinal) ||
               lowered.Contains('<', StringComparison.Ordinal) ||
               lowered.Contains('`', StringComparison.Ordinal) ||
               lowered.Contains("$(", StringComparison.Ordinal);
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

    private sealed class NormalizedIntent
    {
        public NormalizedIntent(string executable, IReadOnlyList<string> args, IReadOnlyList<string> tokens, bool containsShellMetaSyntax)
        {
            Executable = executable;
            Args = args;
            Tokens = tokens;
            ContainsShellMetaSyntax = containsShellMetaSyntax;
        }

        public string Executable { get; }
        public IReadOnlyList<string> Args { get; }
        public IReadOnlyList<string> Tokens { get; }
        public bool ContainsShellMetaSyntax { get; }
    }
}
