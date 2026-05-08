using System.Security.Cryptography;

namespace LocalCursorAgent.Security;

public sealed partial class PatchSafetyGate
{
    private static string ClassifyPatchText(string patchText)
    {
        var text = patchText.Trim();
        if (text.Length == 0)
            return PermissionReasonCodes.PatchInvalidFormat;

        var hasBegin = text.Contains("*** Begin Patch", StringComparison.Ordinal);
        var hasEnd = text.Contains("*** End Patch", StringComparison.Ordinal);
        if (hasBegin && !hasEnd)
            return PermissionReasonCodes.PatchUnexpectedEndOfPatch;
        if (hasEnd && !hasBegin)
            return PermissionReasonCodes.PatchInvalidFormat;

        return PermissionReasonCodes.Allowed;
    }

    private static string ClassifyApplyException(Exception ex)
    {
        var msg = ex.Message ?? string.Empty;
        if (msg.IndexOf("context", StringComparison.OrdinalIgnoreCase) >= 0 &&
            msg.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0)
            return PermissionReasonCodes.PatchContextNotFound;
        if (msg.IndexOf("ambiguous", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("multiple matches", StringComparison.OrdinalIgnoreCase) >= 0)
            return PermissionReasonCodes.PatchAmbiguousMatch;
        if (msg.IndexOf("hunk", StringComparison.OrdinalIgnoreCase) >= 0 &&
            (msg.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 || msg.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0))
            return PermissionReasonCodes.PatchInvalidHunk;
        if (msg.IndexOf("unexpected end", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("eof", StringComparison.OrdinalIgnoreCase) >= 0)
            return PermissionReasonCodes.PatchUnexpectedEndOfPatch;
        if (msg.IndexOf("format", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("parse", StringComparison.OrdinalIgnoreCase) >= 0)
            return PermissionReasonCodes.PatchInvalidFormat;

        return PermissionReasonCodes.PatchApplyFailed;
    }

    private PatchPreviewResult Reject(string reasonCode, string message, string targetPath, string hash = "")
    {
        return new PatchPreviewResult
        {
            PreviewGenerated = true,
            PreviewRejected = true,
            AnchorFound = false,
            FileUnchangedSinceRead = false,
            ReasonCode = reasonCode,
            Message = message,
            TargetPath = targetPath,
            SnapshotHashBeforeApply = hash
        };
    }

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var fullPath = Path.IsPathFullyQualified(path)
            ? path
            : Path.Combine(_session.ActiveWorkspaceRoot, path);

        return Path.GetFullPath(fullPath);
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static string ComputePathHash(string path)
    {
        if (File.Exists(path))
        {
            var bytes = File.ReadAllBytes(path);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        if (Directory.Exists(path))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join("|", Directory.GetFiles(path, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)));
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        return string.Empty;
    }

    private static bool AnchorExists(string targetPath, string anchorHint)
    {
        if (string.IsNullOrWhiteSpace(anchorHint) || !File.Exists(targetPath))
            return true;

        var content = File.ReadAllText(targetPath);
        return content.Contains(anchorHint, StringComparison.Ordinal);
    }

    private static bool CheckUnchangedSinceRead(string targetPath, string snapshotHash)
    {
        if (string.IsNullOrWhiteSpace(snapshotHash))
            return false;

        var currentHash = ComputePathHash(targetPath);
        return string.Equals(currentHash, snapshotHash, StringComparison.OrdinalIgnoreCase);
    }
}
