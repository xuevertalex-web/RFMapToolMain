namespace LocalCursorAgent.Security;

public enum ToolActionKind
{
    ReadFile,
    ListDirectory,
    SearchFiles,
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
