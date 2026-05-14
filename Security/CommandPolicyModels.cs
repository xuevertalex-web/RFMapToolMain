using System;
using System.Collections.Generic;

namespace LocalCursorAgent.Security;

internal static class CommandPolicyCategory
{
    public const string Allowed = "allowed";
    public const string HighRiskApprovalRequired = "high_risk_approval_required";
    public const string HardBlocked = "hard_blocked";
    public const string InvalidMalformed = "invalid_malformed";
    public const string UnsupportedShellMetaSyntax = "unsupported_shell_meta_syntax";
}

internal sealed class CommandPolicyInput
{
    public string? Executable { get; init; }
    public IReadOnlyList<string>? Args { get; init; }
    public string? RawCommandText { get; init; }
    public string? WorkingDirectory { get; init; }
    public ToolActionKind? CommandKind { get; init; }
    public string? Source { get; init; }
}

internal sealed class CommandPolicyDecision
{
    public string Category { get; init; } = CommandPolicyCategory.InvalidMalformed;
    public string ReasonCode { get; init; } = string.Empty;
    public string ReasonMessage { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = "medium";
    public bool ApprovalRequired { get; init; }
    public bool HardBlocked { get; init; }
    public string NormalizedExecutable { get; init; } = string.Empty;
    public IReadOnlyList<string> NormalizedArgs { get; init; } = Array.Empty<string>();
}
