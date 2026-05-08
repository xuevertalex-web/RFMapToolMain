namespace LocalCursorAgent.Security;

internal static class DestructiveOperationReasonCodes
{
    public static string ResolveRollbackReasonCode(bool isMove)
    {
        return isMove ? PermissionReasonCodes.MoveRollbackFailed : PermissionReasonCodes.RenameRollbackFailed;
    }
}
