namespace LocalCursorAgent.Security;

public enum ToolActionKind
{
    ReadFile,
    ListDirectory,
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
