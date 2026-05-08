using System.Text;
using System.Diagnostics;
using LocalCursorAgent.Configuration;
using LocalCursorAgent.Context;
using LocalCursorAgent.Core;
using LocalCursorAgent.Diagnostics;
using LocalCursorAgent.Embeddings;
using LocalCursorAgent.Execution;
using LocalCursorAgent.Indexing;
using LocalCursorAgent.LLM;
using LocalCursorAgent.Memory;
using LocalCursorAgent.Security;
using LocalCursorAgent.Tools;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var parsed = ProgramArgumentParser.ParseArgs(args);
var appRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
var defaultPolicyPath = ProgramWorkspaceHelpers.FindDefaultWorkspacePolicyPath(appRoot);
var policySourcePath = parsed.WorkspacePolicyPath ?? defaultPolicyPath;
var policyConfig = ProgramWorkspacePolicyLoader.LoadWorkspacePolicy(policySourcePath);
if (!policyConfig.Success)
{
    Console.WriteLine($"Workspace policy load failed [{policyConfig.ReasonCodeName} / {policyConfig.ReasonCode}]: {policyConfig.Message}");
    return;
}

parsed = parsed with
{
    WorkspacePath = parsed.WorkspacePath ?? policyConfig.Policy?.WorkspacePath,
    AccessMode = policyConfig.Policy?.AccessMode ?? parsed.AccessMode
};

var runtimeRoot = AgentRuntimePaths.ResolveRuntimeRoot(AppContext.BaseDirectory);
AgentRuntimePaths.EnsureRuntimeRootPrepared(runtimeRoot);
var tracer = new ExecutionTracer(runtimeRoot);
tracer.LogActionEvent("AppEntry", "Program", ExecutionTracer.ActionLogLevel.Info, "started", metadata: new Dictionary<string, object?>
{
    { "args", args },
    { "runtime_root", runtimeRoot }
});
StartParentProcessWatchdog(parsed.ParentPid, tracer);

var workspaceResolver = new WorkspaceContextService();
var workspaceAllowRoots = ProgramWorkspaceHelpers.MergeDistinct(parsed.WorkspaceAllowRoots, policyConfig.Policy?.WorkspaceAllowRoots);
var workspaceDenyRoots = ProgramWorkspaceHelpers.MergeDistinct(parsed.WorkspaceDenyRoots, policyConfig.Policy?.WorkspaceDenyRoots);
var taskWorkspaceHint = parsed.WorkspacePath is null ? ProgramWorkspaceHelpers.ExtractWorkspacePathFromTask(parsed.Task) : null;
var workspaceResolution = workspaceResolver.Resolve(parsed.WorkspacePath, appRoot, runtimeRoot, workspaceAllowRoots, workspaceDenyRoots);
tracer.LogActionEvent("WorkspaceResolution", "Program", ExecutionTracer.ActionLogLevel.Info, workspaceResolution.Success ? "resolved" : "failed", workspaceResolution.ReasonCode, new Dictionary<string, object?>
{
    { "requested_workspace", parsed.WorkspacePath ?? string.Empty },
    { "task_workspace_hint", taskWorkspaceHint ?? string.Empty },
    { "workspace_root", workspaceResolution.WorkspaceRoot ?? string.Empty },
    { "message", workspaceResolution.Message }
});

if (string.IsNullOrWhiteSpace(parsed.WorkspacePath) &&
    !string.IsNullOrWhiteSpace(taskWorkspaceHint))
{
    var hintedResolution = workspaceResolver.Resolve(taskWorkspaceHint, appRoot, runtimeRoot, workspaceAllowRoots, workspaceDenyRoots);
    if (hintedResolution.Success && !string.IsNullOrWhiteSpace(hintedResolution.WorkspaceRoot))
    {
        workspaceResolution = hintedResolution;
        parsed = parsed with { WorkspacePath = taskWorkspaceHint };
    }
}

if (parsed.Help)
{
    PrintUsage();
    return;
}

if (!workspaceResolution.Success || string.IsNullOrWhiteSpace(workspaceResolution.WorkspaceRoot))
{
    if (!string.IsNullOrWhiteSpace(parsed.WorkspacePath) && workspaceResolution.Reason == WorkspaceResolutionReasonCode.WorkspaceNotAllowed)
    {
        Console.WriteLine($"Rejected workspace request: '{parsed.WorkspacePath}' is outside the configured workspace allowlist.");
    }
    else if (!string.IsNullOrWhiteSpace(parsed.WorkspacePath) && workspaceResolution.Reason == WorkspaceResolutionReasonCode.WorkspaceRootProtected)
    {
        Console.WriteLine($"Rejected workspace request: '{parsed.WorkspacePath}' resolves into the agent runtime area.");
    }
    else if (!string.IsNullOrWhiteSpace(parsed.WorkspacePath) && workspaceResolution.Reason == WorkspaceResolutionReasonCode.WorkspaceDeniedByPolicy)
    {
        Console.WriteLine($"Rejected workspace request: '{parsed.WorkspacePath}' is blocked by policy.");
    }

    Console.WriteLine($"Workspace resolution failed [{workspaceResolution.ReasonCodeName} / {workspaceResolution.ReasonCode}]: {workspaceResolution.Message}");
    return;
}

var workspaceRoot = workspaceResolution.WorkspaceRoot;
var protectedRoots = AgentRuntimePaths.DefaultProtectedRoots(runtimeRoot, appRoot).ToList();
var protectedPolicy = new ProtectedPathPolicy(protectedRoots);

var session = new AgentSessionContext
{
    SessionId = Guid.NewGuid().ToString("N"),
    RuntimeRoot = runtimeRoot,
    ActiveWorkspaceRoot = workspaceRoot,
    AccessMode = parsed.AccessMode,
    ProtectedPathPolicy = protectedPolicy
};

Console.WriteLine($"RuntimeRoot: {runtimeRoot}");
Console.WriteLine($"WorkspaceRoot: {workspaceRoot}");
Console.WriteLine($"AccessMode: {parsed.AccessMode}");
Console.WriteLine($"AccessModeDescription: {DescribeAccessMode(parsed.AccessMode)}");
Console.WriteLine($"ProtectedRootsCount: {protectedRoots.Count}");
Console.WriteLine($"WorkspacePolicy: {(string.IsNullOrWhiteSpace(policySourcePath) ? "none" : policySourcePath)}");

var llmClient = CreateLlmClient(parsed.LlmProvider, parsed.OllamaModel, appRoot);
var permissionGuard = new PermissionGuard();
var toolRegistry = new ToolRegistry();
var safeProcessRunner = new SafeProcessRunner(session, permissionGuard);
var patchSafetyGate = new PatchSafetyGate(session, permissionGuard);
var destructiveOperationSafetyGate = new DestructiveOperationSafetyGate(session, permissionGuard, tracer);
var sandboxManager = new SandboxManager(workspaceRoot, runtimeRoot);

var fileActionFactory = CreateFileActionFactory(workspaceRoot);
var buildActionFactory = CreateBuildActionFactory(workspaceRoot);
tracer.LogWorkspaceResolution(parsed.WorkspacePath ?? appRoot, workspaceResolution, runtimeRoot);
tracer.LogSessionHeader(session, protectedRoots);

toolRegistry.Register(new GuardedTool(
    new FileTool(session, permissionGuard, patchSafetyGate, destructiveOperationSafetyGate, sandboxManager, tracer),
    permissionGuard,
    session,
    fileActionFactory,
    tracer));

toolRegistry.Register(new GuardedTool(
    new BuildTool(session, permissionGuard, tracer),
    permissionGuard,
    session,
    buildActionFactory,
    tracer));

var memory = new MemoryStore();
memory.AddWorkspaceResolution(workspaceResolution, runtimeRoot, parsed.WorkspacePath ?? appRoot);
memory.AddSessionHeader(session, protectedRoots);
var buildVerifier = new BuildVerifier(safeProcessRunner, tracer);
var agentConfig = new AgentConfig(workspaceRoot);
var embeddingService = new EmbeddingService(disabled: agentConfig.DisableEmbeddings);
Console.WriteLine($"EmbeddingsStatus: {embeddingService.DescribeStatus()}");
tracer.UpdateRunEmbeddingStatus(embeddingService.DescribeStatus(), embeddingService.Status != EmbeddingRuntimeStatus.Enabled);
tracer.LogActionEvent("EmbeddingStatus", "Program", ExecutionTracer.ActionLogLevel.Info, embeddingService.DescribeStatus(), metadata: new Dictionary<string, object?>
{
    { "degraded", embeddingService.Status != EmbeddingRuntimeStatus.Enabled }
});
var fileStateManager = new FileStateManager();
var vectorStore = new VectorStore();
var projectIndexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, agentConfig, fileStateManager);
var contextBuilder = new ContextBuilder(workspaceRoot, vectorStore, fileStateManager, new ProjectSymbolDirectory(), tracer);
var agent = new Agent(llmClient, toolRegistry, memory, buildVerifier, sandboxManager, projectIndexer, contextBuilder, fileStateManager, session, workspaceResolution);

if (string.IsNullOrWhiteSpace(parsed.Task))
{
    Console.Write("Task: ");
    parsed = parsed with { Task = Console.ReadLine() ?? string.Empty };
}

if (string.IsNullOrWhiteSpace(parsed.Task))
{
    Console.WriteLine("No task provided.");
    return;
}

var normalizedTask = parsed.Task.Trim();
tracer.StartRun(
    parsed.Task,
    normalizedTask,
    workspaceRoot,
    runtimeRoot,
    parsed.AccessMode.ToString(),
    llmClient.GetType().Name,
    parsed.OllamaModel ?? Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? string.Empty);
tracer.UpdateRunSession(session.SessionId);
tracer.LogActionEvent("TaskReceived", "Program", ExecutionTracer.ActionLogLevel.Info, "accepted", metadata: new Dictionary<string, object?>
{
    { "task_raw", parsed.Task },
    { "task_normalized", normalizedTask }
});
tracer.LogActionEvent("PermissionContextCreated", "Program", ExecutionTracer.ActionLogLevel.Info, "created", metadata: new Dictionary<string, object?>
{
    { "session_id", session.SessionId },
    { "workspace_root", session.ActiveWorkspaceRoot },
    { "access_mode", session.AccessMode.ToString() },
    { "protected_roots_count", protectedRoots.Count }
});

var result = await agent.RunTask(parsed.Task);
tracer.LogActionEvent("ResultEmitted", "Program", ExecutionTracer.ActionLogLevel.Info, "emitted", metadata: new Dictionary<string, object?>
{
    { "result_preview", result.Length > 200 ? result[..200] : result }
});
var learningExtractor = new RunFailureMemoryExtractor(runtimeRoot);
var learningCapture = learningExtractor.CaptureLatestRun();
tracer.LogActionEvent("LearningCapture", "Program", learningCapture.Captured ? ExecutionTracer.ActionLogLevel.Info : ExecutionTracer.ActionLogLevel.Warning, learningCapture.Captured ? "captured" : "skipped", metadata: new Dictionary<string, object?>
{
    { "reason", learningCapture.Reason },
    { "outcome_class", learningCapture.Record?.OutcomeClass ?? string.Empty }
});
var strategyWriter = new RunStrategyFeedbackWriter(runtimeRoot);
var strategyResult = strategyWriter.Rebuild();
tracer.LogActionEvent("StrategyFeedback", "Program", strategyResult.Written ? ExecutionTracer.ActionLogLevel.Info : ExecutionTracer.ActionLogLevel.Warning, strategyResult.Written ? "written" : "skipped", metadata: new Dictionary<string, object?>
{
    { "strict_targeting_bias", strategyResult.Profile.StrictTargetingBias },
    { "single_file_bias", strategyResult.Profile.SingleFileBias },
    { "early_stop_bias", strategyResult.Profile.EarlyStopBias },
    { "dominant_failure_classes", strategyResult.Profile.DominantFailureClasses }
});
var adaptivePreviewWriter = new AdaptiveGateTuningPreviewWriter(runtimeRoot);
var adaptivePreview = adaptivePreviewWriter.Rebuild();
tracer.LogActionEvent("AdaptiveGateTuningPreview", "Program", adaptivePreview.Written ? ExecutionTracer.ActionLogLevel.Info : ExecutionTracer.ActionLogLevel.Warning, adaptivePreview.Written ? "written" : "skipped", metadata: new Dictionary<string, object?>
{
    { "target_confidence_floor", adaptivePreview.Preview.RecommendedTargetConfidenceFloor },
    { "semantic_fallback_allowed", adaptivePreview.Preview.RecommendedSemanticFallbackAllowed },
    { "single_file_preference", adaptivePreview.Preview.RecommendedSingleFilePreference },
    { "max_safe_file_count", adaptivePreview.Preview.RecommendedMaxSafeFileCount },
    { "early_stop_strictness", adaptivePreview.Preview.RecommendedEarlyStopStrictness }
});
Console.WriteLine("__LOCAL_CURSOR_AGENT_RESULT_START__");
Console.WriteLine(result);
Console.WriteLine("__LOCAL_CURSOR_AGENT_RESULT_END__");
Console.WriteLine($"Latest manifest: {tracer.GetLatestManifestPath()}");

static Func<string, ToolAction> CreateFileActionFactory(string workspaceRoot) => input =>
{
    if (input.StartsWith("read:", StringComparison.OrdinalIgnoreCase))
    {
        var path = input[5..].Trim();
        return new ToolAction
        {
            Kind = ToolActionKind.ReadFile,
            TargetPath = ProgramPathParsingHelpers.ResolveTargetPath(workspaceRoot, path)
        };
    }

    if (input.StartsWith("write:", StringComparison.OrdinalIgnoreCase))
    {
        var payload = input[6..];
        var separator = ProgramPathParsingHelpers.FindWriteSeparator(payload);
        var path = separator >= 0 ? payload[..separator].Trim() : payload.Trim();
        var content = separator >= 0 ? payload[(separator + 1)..] : string.Empty;

        return new ToolAction
        {
            Kind = ToolActionKind.WriteFile,
            TargetPath = ProgramPathParsingHelpers.ResolveTargetPath(workspaceRoot, path),
            Payload = content
        };
    }

    if (input.StartsWith("delete:", StringComparison.OrdinalIgnoreCase))
    {
        var path = input[7..].Trim();
        return new ToolAction
        {
            Kind = ToolActionKind.DeleteFile,
            TargetPath = ProgramPathParsingHelpers.ResolveTargetPath(workspaceRoot, path)
        };
    }

    if (input.StartsWith("rename:", StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith("move:", StringComparison.OrdinalIgnoreCase))
    {
        var isMove = input.StartsWith("move:", StringComparison.OrdinalIgnoreCase);
        var payload = input[(isMove ? 5 : 7)..];
        var separator = ProgramPathParsingHelpers.FindPathPairSeparator(payload);
        var source = separator >= 0 ? payload[..separator].Trim() : payload.Trim();
        var destination = separator >= 0 ? payload[(separator + 1)..].Trim() : string.Empty;

        return new ToolAction
        {
            Kind = isMove ? ToolActionKind.MoveFile : ToolActionKind.RenameFile,
            SourcePath = ProgramPathParsingHelpers.ResolveTargetPath(workspaceRoot, source),
            DestinationPath = ProgramPathParsingHelpers.ResolveTargetPath(workspaceRoot, destination)
        };
    }

    return new ToolAction { Kind = ToolActionKind.RunCommand };
};

static Func<string, ToolAction> CreateBuildActionFactory(string workspaceRoot) => input =>
{
    var target = string.IsNullOrWhiteSpace(input) ? workspaceRoot : input.Trim();
    if (target.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
        target.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
    {
        target = Path.GetDirectoryName(target) ?? target;
    }
    return new ToolAction
    {
        Kind = ToolActionKind.Build,
        WorkingDirectory = ProgramPathParsingHelpers.ResolveTargetPath(workspaceRoot, target)
    };
};


static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run -- --workspace <path> --task <task> [--access ReadOnly|WorkspaceWrite|WorkspaceFullAccess]");
    Console.WriteLine("  dotnet run -- --workspace-policy <policy.json> --task <task>");
    Console.WriteLine("  dotnet run -- --workspace <path> --workspace-allow <allowed-root> --task <task>");
    Console.WriteLine("  dotnet run -- --workspace <path> --workspace-deny <denied-root> --task <task>");
    Console.WriteLine("  dotnet run -- --llm-provider <openai|local|hybrid> --task <task>");
    Console.WriteLine("  dotnet run -- --parent-pid <pid> --task <task>");
    Console.WriteLine("  dotnet run -- --task <task>");
}

static void StartParentProcessWatchdog(int? parentPid, ExecutionTracer tracer)
{
    if (!parentPid.HasValue || parentPid.Value <= 0)
        return;

    Process? parentProcess;
    try
    {
        parentProcess = Process.GetProcessById(parentPid.Value);
    }
    catch
    {
        tracer.LogActionEvent("ParentWatchdog", "Program", ExecutionTracer.ActionLogLevel.Warning, "parent_missing", "PARENT_PROCESS_NOT_FOUND", new Dictionary<string, object?>
        {
            { "parent_pid", parentPid.Value }
        });
        Environment.Exit(0);
        return;
    }

    tracer.LogActionEvent("ParentWatchdog", "Program", ExecutionTracer.ActionLogLevel.Info, "armed", metadata: new Dictionary<string, object?>
    {
        { "parent_pid", parentPid.Value },
        { "parent_name", parentProcess.ProcessName }
    });

    _ = Task.Run(async () =>
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));

            var parentAlive = true;
            try
            {
                parentAlive = !parentProcess.HasExited;
            }
            catch
            {
                parentAlive = false;
            }

            if (parentAlive)
                continue;

            tracer.LogActionEvent("ParentWatchdog", "Program", ExecutionTracer.ActionLogLevel.Warning, "parent_exited", "PARENT_PROCESS_EXITED", new Dictionary<string, object?>
            {
                { "parent_pid", parentPid.Value }
            });
            Environment.Exit(0);
        }
    });
}

static string DescribeAccessMode(AgentAccessMode accessMode) => accessMode switch
{
    AgentAccessMode.ReadOnly => "Analysis only; no writes or destructive actions.",
    AgentAccessMode.WorkspaceWrite => "Write/patch allowed inside workspace; destructive actions denied.",
    AgentAccessMode.WorkspaceFullAccess => "Full engineering access inside workspace; runtime and protected paths remain denied.",
    _ => "Unknown access mode."
};

static ILLMClient CreateLlmClient(string? providerOverride, string? ollamaModelOverride, string appRoot)
{
    return LocalCursorAgent.LLM.Runtime.LlmRuntimeFactory.Create(providerOverride, ollamaModelOverride, appRoot);
}


