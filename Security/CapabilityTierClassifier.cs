namespace LocalCursorAgent.Security;

internal static class CapabilityTierClassifier
{
    public static CapabilityAssessment Classify(ToolAction action)
    {
        return action.Kind switch
        {
            ToolActionKind.ReadFile or ToolActionKind.ListDirectory or ToolActionKind.SearchFiles => Create(
                action.Kind,
                CapabilityClass.Analysis,
                CapabilityTier.Analysis,
                CapabilityGate.Allowed,
                PermissionReasonCodes.Allowed,
                "Read-only workspace inspection action."),

            ToolActionKind.WriteFile or ToolActionKind.CreateFile or ToolActionKind.PatchFile or ToolActionKind.Build or ToolActionKind.Test => Create(
                action.Kind,
                CapabilityClass.Mutation,
                CapabilityTier.Mutation,
                CapabilityGate.Allowed,
                PermissionReasonCodes.Allowed,
                "Non-destructive workspace mutation action."),

            ToolActionKind.DeleteFile or ToolActionKind.RenameFile or ToolActionKind.MoveFile => Create(
                action.Kind,
                CapabilityClass.Destructive,
                CapabilityTier.Destructive,
                CapabilityGate.ApprovalRequired,
                PermissionReasonCodes.AccessDeniedDeleteOperation,
                "Destructive mutation action requires explicit approval."),

            ToolActionKind.RunCommand => ClassifyCommand(action),

            _ => Create(
                action.Kind,
                CapabilityClass.KernelSystem,
                CapabilityTier.System,
                CapabilityGate.Denied,
                PermissionReasonCodes.ToolDeniedByPolicy,
                "Unsupported action kind.")
        };
    }

    public static bool RequiresEscalation(CapabilityAssessment fromAssessment, CapabilityAssessment toAssessment)
    {
        return toAssessment.CapabilityTier > fromAssessment.CapabilityTier;
    }

    private static CapabilityAssessment ClassifyCommand(ToolAction action)
    {
        var policyInput = !string.IsNullOrWhiteSpace(action.CommandExecutable) || action.CommandArgs is { Count: > 0 }
            ? new CommandPolicyInput
            {
                Executable = action.CommandExecutable,
                Args = action.CommandArgs,
                RawCommandText = action.Payload,
                WorkingDirectory = action.WorkingDirectory,
                CommandKind = action.Kind,
                Source = "capability_classifier"
            }
            : CommandRiskPolicy.BuildInputFromRawCommand(action.Payload, action.WorkingDirectory, action.Kind, "capability_classifier");
        var decision = CommandRiskPolicy.Evaluate(policyInput);

        return decision.Category switch
        {
            var category when category == CommandPolicyCategory.Allowed => Create(
                action.Kind,
                CapabilityClass.Analysis,
                CapabilityTier.Analysis,
                CapabilityGate.Allowed,
                PermissionReasonCodes.Allowed,
                "Command is allowed by canonical policy.",
                decision.Category,
                decision.RiskLevel),

            var category when category == CommandPolicyCategory.HighRiskApprovalRequired => Create(
                action.Kind,
                CapabilityClass.KernelSystem,
                CapabilityTier.System,
                CapabilityGate.ApprovalRequired,
                PermissionReasonCodes.HighRiskApprovalRequired,
                "High-risk command capability requires explicit approval.",
                decision.Category,
                decision.RiskLevel),

            var category when category == CommandPolicyCategory.HardBlocked => Create(
                action.Kind,
                CapabilityClass.KernelSystem,
                CapabilityTier.System,
                CapabilityGate.Denied,
                PermissionReasonCodes.CommandHardBlocked,
                "Command is hard-blocked by canonical policy.",
                decision.Category,
                decision.RiskLevel),

            var category when category == CommandPolicyCategory.UnsupportedShellMetaSyntax => Create(
                action.Kind,
                CapabilityClass.KernelSystem,
                CapabilityTier.System,
                CapabilityGate.Denied,
                PermissionReasonCodes.CommandUnsupportedShellSyntax,
                "Unsupported shell/meta command syntax.",
                decision.Category,
                decision.RiskLevel),

            _ => Create(
                action.Kind,
                CapabilityClass.KernelSystem,
                CapabilityTier.System,
                CapabilityGate.Denied,
                PermissionReasonCodes.CommandMalformed,
                "Malformed command capability request.",
                decision.Category,
                decision.RiskLevel)
        };
    }

    private static CapabilityAssessment Create(
        ToolActionKind kind,
        string capabilityClass,
        int capabilityTier,
        string gate,
        string reasonCode,
        string reasonMessage,
        string? commandPolicyCategory = null,
        string? commandRiskLevel = null)
    {
        return new CapabilityAssessment
        {
            ActionKind = kind,
            CapabilityClass = capabilityClass,
            CapabilityTier = capabilityTier,
            Gate = gate,
            ReasonCode = reasonCode,
            ReasonMessage = reasonMessage,
            CommandPolicyCategory = commandPolicyCategory,
            CommandRiskLevel = commandRiskLevel
        };
    }
}
