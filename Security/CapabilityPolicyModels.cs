namespace LocalCursorAgent.Security;

internal static class CapabilityClass
{
    public const string Analysis = "analysis";
    public const string Mutation = "mutation";
    public const string Destructive = "destructive";
    public const string Persistence = "persistence";
    public const string CredentialSensitive = "credential_sensitive";
    public const string ReverseEngineering = "reverse_engineering";
    public const string KernelSystem = "kernel_system";
}

internal static class CapabilityTier
{
    public const int Analysis = 0;
    public const int Mutation = 1;
    public const int Destructive = 2;
    public const int Persistence = 3;
    public const int Sensitive = 4;
    public const int System = 5;
}

internal static class CapabilityGate
{
    public const string Allowed = "allowed";
    public const string ApprovalRequired = "approval_required";
    public const string Denied = "denied";
}

internal sealed class CapabilityAssessment
{
    public ToolActionKind ActionKind { get; init; }
    public string CapabilityClass { get; init; } = global::LocalCursorAgent.Security.CapabilityClass.Analysis;
    public int CapabilityTier { get; init; } = global::LocalCursorAgent.Security.CapabilityTier.Analysis;
    public string Gate { get; init; } = CapabilityGate.Allowed;
    public string ReasonCode { get; init; } = string.Empty;
    public string ReasonMessage { get; init; } = string.Empty;
    public string? CommandPolicyCategory { get; init; }
    public string? CommandRiskLevel { get; init; }
}
