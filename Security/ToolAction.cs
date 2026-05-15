using System.Collections.Generic;

namespace LocalCursorAgent.Security;

public sealed class ToolAction
{
    public ToolActionKind Kind { get; init; }
    public string? RunId { get; init; }
    public string? TargetPath { get; init; }
    public string? SourcePath { get; init; }
    public string? DestinationPath { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? CommandExecutable { get; init; }
    public IReadOnlyList<string>? CommandArgs { get; init; }
    public string? Payload { get; init; }
}
