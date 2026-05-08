using System.Text.Json;
using System.Text.Json.Serialization;
using LocalCursorAgent.Security;

internal static class ProgramWorkspacePolicyLoader
{
    public static WorkspacePolicyLoadResult LoadWorkspacePolicy(string? policyPath)
    {
        if (string.IsNullOrWhiteSpace(policyPath))
            return WorkspacePolicyLoadResult.CreateSuccess(null);

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(policyPath);
        }
        catch
        {
            return WorkspacePolicyLoadResult.Fail(PermissionReasonCodes.WorkspacePolicyInvalid, "Workspace policy path could not be normalized.");
        }

        if (!File.Exists(fullPath))
            return WorkspacePolicyLoadResult.Fail(PermissionReasonCodes.WorkspacePolicyNotFound, $"Workspace policy file not found: {fullPath}");

        try
        {
            var json = File.ReadAllText(fullPath);
            var policy = JsonSerializer.Deserialize<WorkspacePolicyFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true)
                }
            });

            return WorkspacePolicyLoadResult.CreateSuccess(policy);
        }
        catch (Exception ex)
        {
            return WorkspacePolicyLoadResult.Fail(PermissionReasonCodes.WorkspacePolicyInvalid, $"Workspace policy could not be parsed: {ex.Message}");
        }
    }
}
