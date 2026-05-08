using LocalCursorAgent.Security;

internal static class ProgramArgumentParser
{
    public static ParsedArgs ParseArgs(string[] args)
    {
        string? workspace = null;
        string? task = null;
        string? workspacePolicy = null;
        string? llmProvider = null;
        string? ollamaModel = null;
        int? parentPid = null;
        AgentAccessMode accessMode = AgentAccessMode.WorkspaceWrite;
        bool help = false;
        var workspaceAllowRoots = new List<string>();
        var workspaceDenyRoots = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--workspace", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                workspace = args[++i];
            else if (arg.Equals("--workspace-policy", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                workspacePolicy = args[++i];
            else if (arg.Equals("--workspace-allow", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                workspaceAllowRoots.Add(args[++i]);
            else if (arg.Equals("--workspace-deny", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                workspaceDenyRoots.Add(args[++i]);
            else if (arg.Equals("--llm-provider", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                llmProvider = args[++i];
            else if (arg.Equals("--ollama-model", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                ollamaModel = args[++i];
            else if (arg.Equals("--parent-pid", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var pidText = args[++i];
                if (int.TryParse(pidText, out var parsedPid) && parsedPid > 0)
                    parentPid = parsedPid;
            }
            else if (arg.Equals("--task", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                task = args[++i];
            else if (arg.Equals("--access", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var modeText = args[++i];
                if (!Enum.TryParse<AgentAccessMode>(modeText, true, out accessMode))
                    accessMode = AgentAccessMode.WorkspaceWrite;
            }
            else if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
                help = true;
            else if (task == null)
                task = arg;
        }

        return new ParsedArgs
        {
            WorkspacePath = workspace,
            WorkspacePolicyPath = workspacePolicy,
            LlmProvider = llmProvider,
            OllamaModel = ollamaModel,
            ParentPid = parentPid,
            WorkspaceAllowRoots = workspaceAllowRoots,
            WorkspaceDenyRoots = workspaceDenyRoots,
            AccessMode = accessMode,
            Task = task,
            Help = help
        };
    }
}
