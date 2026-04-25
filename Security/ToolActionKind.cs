namespace LocalCursorAgent.Security;

public enum ToolActionKind
{
    ReadFile,
    WriteFile,
    CreateFile,
    PatchFile,
    DeleteFile,
    RenameFile,
    MoveFile,
    Build,
    Test,
    RunCommand
}
