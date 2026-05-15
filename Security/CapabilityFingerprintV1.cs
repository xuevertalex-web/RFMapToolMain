namespace LocalCursorAgent.Security;

public static class CapabilityActionProfile
{
    public const string ReadFile = "read_file";
    public const string WriteFile = "write_file";
    public const string CreateFile = "create_file";
    public const string PatchFile = "patch_file";
    public const string DeleteFile = "delete_file";
    public const string RenameFile = "rename_file";
    public const string MoveFile = "move_file";
    public const string Build = "build";
    public const string Test = "test";
    public const string RunCommand = "run_command";
}

public sealed class CapabilityFingerprintV1
{
    public const int CurrentVersion = 1;

    public int FingerprintVersion { get; init; } = CurrentVersion;
    public string ActionKind { get; init; } = string.Empty;
    public string CapabilityClass { get; init; } = global::LocalCursorAgent.Security.CapabilityClass.Analysis;
    public int CapabilityTier { get; init; } = global::LocalCursorAgent.Security.CapabilityTier.Analysis;
    public string CapabilityGate { get; init; } = global::LocalCursorAgent.Security.CapabilityGate.Allowed;
    public string? PolicyCategory { get; init; }
    public string ActionProfile { get; init; } = CapabilityActionProfile.ReadFile;

    internal static CapabilityFingerprintV1 FromAssessment(ToolAction action, CapabilityAssessment assessment)
    {
        return new CapabilityFingerprintV1
        {
            FingerprintVersion = CurrentVersion,
            ActionKind = action.Kind.ToString(),
            CapabilityClass = assessment.CapabilityClass,
            CapabilityTier = assessment.CapabilityTier,
            CapabilityGate = assessment.Gate,
            PolicyCategory = NormalizeOptionalValue(assessment.CommandPolicyCategory),
            ActionProfile = ResolveActionProfile(action.Kind)
        };
    }

    public static bool IsKnownActionProfile(string profile) => profile switch
    {
        CapabilityActionProfile.ReadFile => true,
        CapabilityActionProfile.WriteFile => true,
        CapabilityActionProfile.CreateFile => true,
        CapabilityActionProfile.PatchFile => true,
        CapabilityActionProfile.DeleteFile => true,
        CapabilityActionProfile.RenameFile => true,
        CapabilityActionProfile.MoveFile => true,
        CapabilityActionProfile.Build => true,
        CapabilityActionProfile.Test => true,
        CapabilityActionProfile.RunCommand => true,
        _ => false
    };

    private static string ResolveActionProfile(ToolActionKind kind) => kind switch
    {
        ToolActionKind.ReadFile => CapabilityActionProfile.ReadFile,
        ToolActionKind.WriteFile => CapabilityActionProfile.WriteFile,
        ToolActionKind.CreateFile => CapabilityActionProfile.CreateFile,
        ToolActionKind.PatchFile => CapabilityActionProfile.PatchFile,
        ToolActionKind.DeleteFile => CapabilityActionProfile.DeleteFile,
        ToolActionKind.RenameFile => CapabilityActionProfile.RenameFile,
        ToolActionKind.MoveFile => CapabilityActionProfile.MoveFile,
        ToolActionKind.Build => CapabilityActionProfile.Build,
        ToolActionKind.Test => CapabilityActionProfile.Test,
        ToolActionKind.RunCommand => CapabilityActionProfile.RunCommand,
        _ => CapabilityActionProfile.ReadFile
    };

    private static string? NormalizeOptionalValue(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
