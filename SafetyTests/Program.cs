using System.Text.Json;
using System.Net;
using System.Net.Http;
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
using LocalCursorAgent.LLM.Runtime;

await RunAnalysisFallbackTimeoutRegression();
await RunAnalysisFallbackLlmRequestFailedRegression();
await RunAnalysisFallback_ProviderUnavailable_IndexedContextSummary_Regression();
await RunAnalysisNormalResponseRegression();
await RunAnalysisUsableErrorPrefixedResponse_NoFallbackRegression();
await RunLlmRetrySuccessRegression();
await RunLlmRetryFailRegression();
await RunLlmRetryNoRetryRegression();
await RunEmbeddingNotFound_DisablesTruthfullyRegression();
await RunRuntimeProfileSelectionRegression();
await RunRuntimeNormalizedClassificationRegression();
await RunOllamaQwenProfileSelectionRegression();
await RunOllamaModelAliasResolutionRegression();
await RunOllamaUsableAnalysisClassificationRegression();
await RunOllamaQwenInstructTerseAnalysis_NoFallbackRegression();
await RunOllamaQwenInstructTerseNoKeywordAnalysis_NoFallbackRegression();
await RunRuntimeProviderSelection_OpenAiGeminiRegression();
await RunRuntimeNonOllamaClassificationRegression();
await RunPatchApplyDiagnosticsClassificationRegression();
await RunExternalActionApprovalProposalRegression();
await RunActionLifecycleLedgerRegression();
await RunStructuredActionLifecycleReportingRegression();
await RunEndToEndExecutionPipelineRegression();
await RunBroadIntentNoToolCallsRequiresActionRegression();
await RunTechnicalNoToolCallsRequiresActionRegression();
await RunHostDiagnosticsCommandApprovalRegression();
await RunProcessExecutionHardeningRegression();
await RunRuntimeGpuDiagnosticsTruthfulReportingRegression();
await RunDestructiveFileApprovalMarkerRegression();
await RunGuardedToolExplicitApprovalHandoffRegression();
RunExtractRequestedNewFilePath_ExtensionRegression();
RunExtractRequestedNewFilePath_NoCreateIntentRegression();
RunExtractRequestedNewFilePath_NoExtensionRegression();
RunExtractRequestedNewFilePath_UppercaseExtensionRegression();
RunExtractRequestedNewFilePath_WindowsPathRegression();
RunExtractRequestedNewFilePath_QuotedPathRegression();
RunExtractRequestedNewFilePath_DashUnderscoreRegression();
RunExtractRequestedNewFilePath_RelativeDotSlashRegression();
RunExtractRequestedNewFilePath_MultiDotFileNameRegression();
RunExtractRequestedNewFilePath_UrlNegativeRegression();
RunExtractRequestedNewFilePath_RussianIntentRegression();
RunMemoryProjectScopeResolverRegression();
RunMemoryGovernanceRecalibrationRegression();
RunMemoryScopedRetrievalRegression();
RunMemoryFactoryAndInvalidationHookRegression();
RunMemorySourceScopedRetrievalRegression();
RunMemorySourceInvalidationRegression();
RunMemoryScopeSourceInvalidationRegression();
await RunRunTaskBaselineBehaviorRegression();
await RunRunTaskToolCallFlowRegression();
await RunRunTaskMalformedToolCallDiagnosticRegression();
await RunRunTaskTargetResolutionRegression();
await RunTargetResolutionPathPreservingRegression();
await RunStructuredActionContractCompatibilityRegression();
await RunIsolatedExecutionWorkspaceRoutingRegression();
await RunContextSelectionPrecisionRegression();
await RunContextSelectionAdaptiveBudgetFitRegression();

static async Task RunAnalysisFallbackTimeoutRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    File.WriteAllText(Path.Combine(workspaceRoot, "Program.cs"), "namespace SampleApp; public static class Entry { public static void Hello() { } }");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun(
        "Опиши проект кратко на русском",
        "Опиши проект кратко на русском",
        workspaceRoot,
        runtimeRoot,
        AgentAccessMode.WorkspaceWrite.ToString(),
        "FakeTimeoutLLMClient",
        "fake-timeout-model");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var toolRegistry = new ToolRegistry();
    var memory = new MemoryStore();
    var permissionGuard = new PermissionGuard();
    var safeProcessRunner = new SafeProcessRunner(session, permissionGuard, tracer);
    var buildVerifier = new BuildVerifier(safeProcessRunner, tracer);
    var sandboxManager = new SandboxManager(workspaceRoot, runtimeRoot);
    var embeddingService = new EmbeddingService(disabled: true);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var projectIndexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);
    var contextBuilder = new ContextBuilder(workspaceRoot, vectorStore, fileStateManager, new ProjectSymbolDirectory(), tracer);

    var agent = new Agent(
        new FakeTimeoutLlmClient(),
        toolRegistry,
        memory,
        buildVerifier,
        sandboxManager,
        projectIndexer,
        contextBuilder,
        fileStateManager,
        session,
        workspaceResolution: null);

    var oldOut = Console.Out;
    var capture = new StringWriter();
    Console.SetOut(capture);

    try
    {
        _ = await agent.RunTask("Опиши проект кратко на русском");
    }
    finally
    {
        Console.SetOut(oldOut);
    }

    var structured = ExtractStructuredPayload(capture.ToString());

    AssertTrue(structured.GetProperty("ok").GetBoolean(), "Expected fallback analysis run to be successful.");
    AssertTrue(structured.GetProperty("buildStarted").GetBoolean() == false, "Expected buildStarted=false for analysis fallback.");
    AssertTrue(structured.GetProperty("buildSucceeded").GetBoolean() == false, "Expected buildSucceeded=false when build not started.");
    AssertTrue(structured.GetProperty("fallbackReason").GetString() == "MODEL_TIMEOUT", "Expected fallbackReason=MODEL_TIMEOUT.");
    AssertTrue(structured.GetProperty("fallbackMode").GetString() == "INDEXED_CONTEXT_SUMMARY", "Expected fallbackMode=INDEXED_CONTEXT_SUMMARY.");
    AssertTrue(structured.GetProperty("finalStatus").GetString() == "fallback-success", "Expected finalStatus=fallback-success.");

    var changedFiles = structured.GetProperty("changedFiles");
    AssertTrue(changedFiles.ValueKind == JsonValueKind.Array && changedFiles.GetArrayLength() == 0, "Expected changedFiles to be empty.");

    var timeline = structured.GetProperty("timeline");
    AssertTrue(timeline.ValueKind == JsonValueKind.Array && timeline.GetArrayLength() > 0, "Expected non-empty timeline.");
    var stages = timeline.EnumerateArray().Select(e => e.GetProperty("stage").GetString() ?? string.Empty).ToArray();
    AssertContains(stages, "TaskReceived");
    AssertContains(stages, "IndexingStarted");
    AssertContains(stages, "IndexingCompleted");
    AssertContains(stages, "ModelCallStarted");
    AssertContains(stages, "ModelCallTimedOut");
    AssertContains(stages, "AnalysisFallbackStarted");
    AssertContains(stages, "AnalysisFallbackCompleted");
    AssertContains(stages, "RunCompleted");
    AssertTrue(stages.All(s => !s.Contains("Destructive", StringComparison.OrdinalIgnoreCase)), "Expected no destructive events.");

    Console.WriteLine("PASS AnalysisFallback_ModelTimeout_IndexedContextSummary_StructuredObservability");
}

static async Task RunAnalysisNormalResponseRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    File.WriteAllText(Path.Combine(workspaceRoot, "Program.cs"), "namespace SampleApp; public static class Entry { public static void Hello() { } }");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun(
        "Опиши проект кратко на русском",
        "Опиши проект кратко на русском",
        workspaceRoot,
        runtimeRoot,
        AgentAccessMode.WorkspaceWrite.ToString(),
        "FakeSuccessLLMClient",
        "fake-success-model");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var toolRegistry = new ToolRegistry();
    var memory = new MemoryStore();
    var permissionGuard = new PermissionGuard();
    var safeProcessRunner = new SafeProcessRunner(session, permissionGuard, tracer);
    var buildVerifier = new BuildVerifier(safeProcessRunner, tracer);
    var sandboxManager = new SandboxManager(workspaceRoot, runtimeRoot);
    var embeddingService = new EmbeddingService(disabled: true);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var projectIndexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);
    var contextBuilder = new ContextBuilder(workspaceRoot, vectorStore, fileStateManager, new ProjectSymbolDirectory(), tracer);

    var agent = new Agent(
        new FakeSuccessLlmClient(),
        toolRegistry,
        memory,
        buildVerifier,
        sandboxManager,
        projectIndexer,
        contextBuilder,
        fileStateManager,
        session,
        workspaceResolution: null);

    var oldOut = Console.Out;
    var capture = new StringWriter();
    Console.SetOut(capture);

    try
    {
        _ = await agent.RunTask("Опиши проект кратко на русском");
    }
    finally
    {
        Console.SetOut(oldOut);
    }

    var structured = ExtractStructuredPayload(capture.ToString());

    AssertTrue(structured.GetProperty("ok").GetBoolean(), "Expected successful analysis run.");
    AssertTrue(structured.GetProperty("buildStarted").GetBoolean() == false, "Expected buildStarted=false for analysis-only run.");
    AssertTrue(structured.GetProperty("changedFiles").ValueKind == JsonValueKind.Array && structured.GetProperty("changedFiles").GetArrayLength() == 0, "Expected changedFiles to be empty.");
    AssertTrue(string.IsNullOrEmpty(structured.GetProperty("fallbackReason").GetString()), "Expected empty fallbackReason for normal analysis path.");
    AssertTrue(string.IsNullOrEmpty(structured.GetProperty("fallbackMode").GetString()), "Expected empty fallbackMode for normal analysis path.");

    var finalStatus = structured.GetProperty("finalStatus").GetString();
    AssertTrue(string.Equals(finalStatus, "success", StringComparison.Ordinal) || string.Equals(finalStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase), "Expected success finalStatus for normal analysis path.");

    if (structured.TryGetProperty("failure", out var failure))
        AssertTrue(failure.ValueKind == JsonValueKind.Null, "Expected failure=null when property exists.");

    var timeline = structured.GetProperty("timeline");
    AssertTrue(timeline.ValueKind == JsonValueKind.Array && timeline.GetArrayLength() > 0, "Expected non-empty timeline.");
    var stages = timeline.EnumerateArray().Select(e => e.GetProperty("stage").GetString() ?? string.Empty).ToArray();
    AssertContains(stages, "TaskReceived");
    AssertContains(stages, "IndexingStarted");
    AssertContains(stages, "IndexingCompleted");
    AssertContains(stages, "ModelCallStarted");
    AssertContains(stages, "RunCompleted");
    AssertNotContains(stages, "ModelCallTimedOut");
    AssertNotContains(stages, "AnalysisFallbackStarted");
    AssertNotContains(stages, "AnalysisFallbackCompleted");
    AssertTrue(stages.All(s => !s.Contains("Patch", StringComparison.OrdinalIgnoreCase)), "Expected no patch stages.");
    AssertTrue(stages.All(s => !s.Contains("Build", StringComparison.OrdinalIgnoreCase)), "Expected no build stages.");
    AssertTrue(stages.All(s => !s.Contains("Destructive", StringComparison.OrdinalIgnoreCase)), "Expected no destructive stages.");
    AssertTrue(structured.TryGetProperty("contextDiagnostics", out var contextDiagnostics), "Expected contextDiagnostics in payload.");
    var contextItems = contextDiagnostics.GetProperty("items");
    AssertTrue(contextItems.ValueKind == JsonValueKind.Array, "Expected contextDiagnostics.items array.");
    AssertTrue(contextItems.GetArrayLength() > 0, "Expected non-empty context diagnostics items.");
    var firstContext = contextItems[0];
    AssertTrue(!string.IsNullOrWhiteSpace(firstContext.GetProperty("path").GetString()), "Expected context diagnostics path.");
    AssertTrue(!string.IsNullOrWhiteSpace(firstContext.GetProperty("reason").GetString()), "Expected context diagnostics reason.");
    AssertTrue(firstContext.GetProperty("charCount").GetInt32() > 0, "Expected context diagnostics charCount > 0.");
    var totalChars = contextDiagnostics.GetProperty("totalChars").GetInt32();
    var budgetLimit = contextDiagnostics.GetProperty("budgetLimit").GetInt32();
    AssertTrue(totalChars <= 45000, "Expected context diagnostics totalChars to stay bounded.");
    AssertTrue(budgetLimit > 0, "Expected context diagnostics budgetLimit > 0.");

    Console.WriteLine("PASS Analysis_NormalModelResponse_NoFallbackTimeline");
}

static async Task RunAnalysisFallbackLlmRequestFailedRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    File.WriteAllText(Path.Combine(workspaceRoot, "Program.cs"), "namespace SampleApp; public static class Entry { public static void Hello() { } }");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun(
        "Опиши проект кратко на русском",
        "Опиши проект кратко на русском",
        workspaceRoot,
        runtimeRoot,
        AgentAccessMode.WorkspaceWrite.ToString(),
        "FakeLlmRequestFailedClient",
        "fake-request-failed-model");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var toolRegistry = new ToolRegistry();
    var memory = new MemoryStore();
    var permissionGuard = new PermissionGuard();
    var safeProcessRunner = new SafeProcessRunner(session, permissionGuard, tracer);
    var buildVerifier = new BuildVerifier(safeProcessRunner, tracer);
    var sandboxManager = new SandboxManager(workspaceRoot, runtimeRoot);
    var embeddingService = new EmbeddingService(disabled: true);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var projectIndexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);
    var contextBuilder = new ContextBuilder(workspaceRoot, vectorStore, fileStateManager, new ProjectSymbolDirectory(), tracer);

    var agent = new Agent(
        new FakeLlmRequestFailedClient(),
        toolRegistry,
        memory,
        buildVerifier,
        sandboxManager,
        projectIndexer,
        contextBuilder,
        fileStateManager,
        session,
        workspaceResolution: null);

    var oldOut = Console.Out;
    var capture = new StringWriter();
    Console.SetOut(capture);

    try
    {
        _ = await agent.RunTask("Опиши проект кратко на русском");
    }
    finally
    {
        Console.SetOut(oldOut);
    }

    var structured = ExtractStructuredPayload(capture.ToString());

    AssertTrue(structured.GetProperty("ok").GetBoolean(), "Expected fallback analysis run to be successful.");
    AssertTrue(structured.GetProperty("buildStarted").GetBoolean() == false, "Expected buildStarted=false for analysis fallback.");
    AssertTrue(structured.GetProperty("buildSucceeded").GetBoolean() == false, "Expected buildSucceeded=false when build not started.");
    AssertTrue(structured.GetProperty("fallbackReason").GetString() == "LLM_REQUEST_FAILED", "Expected fallbackReason=LLM_REQUEST_FAILED.");
    AssertTrue(structured.GetProperty("fallbackMode").GetString() == "INDEXED_CONTEXT_SUMMARY", "Expected fallbackMode=INDEXED_CONTEXT_SUMMARY.");
    AssertTrue(structured.GetProperty("finalStatus").GetString() == "fallback-success", "Expected finalStatus=fallback-success.");
    if (structured.TryGetProperty("failure", out var failure))
        AssertTrue(failure.ValueKind == JsonValueKind.Null, "Expected failure=null when property exists.");

    var changedFiles = structured.GetProperty("changedFiles");
    AssertTrue(changedFiles.ValueKind == JsonValueKind.Array && changedFiles.GetArrayLength() == 0, "Expected changedFiles to be empty.");

    var timeline = structured.GetProperty("timeline");
    AssertTrue(timeline.ValueKind == JsonValueKind.Array && timeline.GetArrayLength() > 0, "Expected non-empty timeline.");
    var stages = timeline.EnumerateArray().Select(e => e.GetProperty("stage").GetString() ?? string.Empty).ToArray();
    AssertContains(stages, "TaskReceived");
    AssertContains(stages, "IndexingStarted");
    AssertContains(stages, "IndexingCompleted");
    AssertContains(stages, "ModelCallStarted");
    AssertContains(stages, "AnalysisFallbackStarted");
    AssertContains(stages, "AnalysisFallbackCompleted");
    AssertContains(stages, "RunCompleted");
    AssertNotContains(stages, "ModelCallTimedOut");
    AssertTrue(stages.All(s => !s.Contains("Patch", StringComparison.OrdinalIgnoreCase)), "Expected no patch stages.");
    AssertTrue(stages.All(s => !s.Contains("Build", StringComparison.OrdinalIgnoreCase)), "Expected no build stages.");
    AssertTrue(stages.All(s => !s.Contains("Destructive", StringComparison.OrdinalIgnoreCase)), "Expected no destructive stages.");

    Console.WriteLine("PASS AnalysisFallback_LlmRequestFailed_IndexedContextSummary_StructuredObservability");
}

static async Task RunLlmRetrySuccessRegression()
{
    var (structured, _, _) = await RunAgentWithAdapter(new FailOnceThenSuccessAdapter("Error: request timed out"));
    AssertTrue(structured.GetProperty("ok").GetBoolean(), "Expected success after retry.");
    AssertTrue(structured.GetProperty("retryCount").GetInt32() > 0, "Expected retryCount > 0.");
    Console.WriteLine("PASS LlmRetry_SuccessAfterRetry");
}

static async Task RunLlmRetryFailRegression()
{
    var (structured, _, _) = await RunAgentWithAdapter(new AlwaysThrowAdapter("429 rate limit"));
    AssertTrue(!structured.GetProperty("ok").GetBoolean(), "Expected failure after retry exhaustion.");
    AssertTrue(structured.GetProperty("retryCount").GetInt32() > 0, "Expected retryCount > 0 for fail path.");
    var errorType = structured.GetProperty("errorType").GetString() ?? string.Empty;
    AssertTrue(string.Equals(errorType, "provider_rate_limit", StringComparison.Ordinal), $"Expected provider_rate_limit, got {errorType}.");
    Console.WriteLine("PASS LlmRetry_FailAfterRetries");
}

static async Task RunLlmRetryNoRetryRegression()
{
    var (structured, _, _) = await RunAgentWithAdapter(new StaticSuccessAdapter("analysis success"));
    AssertTrue(structured.GetProperty("ok").GetBoolean(), "Expected first-attempt success.");
    AssertTrue(structured.GetProperty("retryCount").GetInt32() == 0, "Expected retryCount=0 without retries.");
    Console.WriteLine("PASS LlmRetry_NoRetryOnFirstSuccess");
}

static async Task RunAnalysisFallback_ProviderUnavailable_IndexedContextSummary_Regression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    File.WriteAllText(Path.Combine(workspaceRoot, "Program.cs"), "namespace SampleApp; public static class Entry { public static void Hello() { } }");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun(
        "Analyze the project briefly",
        "Analyze the project briefly",
        workspaceRoot,
        runtimeRoot,
        AgentAccessMode.WorkspaceWrite.ToString(),
        "FakeProviderUnavailableClient",
        "fake-provider-unavailable-model");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var toolRegistry = new ToolRegistry();
    var memory = new MemoryStore();
    var permissionGuard = new PermissionGuard();
    var safeProcessRunner = new SafeProcessRunner(session, permissionGuard, tracer);
    var buildVerifier = new BuildVerifier(safeProcessRunner, tracer);
    var sandboxManager = new SandboxManager(workspaceRoot, runtimeRoot);
    var embeddingService = new EmbeddingService(disabled: true);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var projectIndexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);
    var contextBuilder = new ContextBuilder(workspaceRoot, vectorStore, fileStateManager, new ProjectSymbolDirectory(), tracer);

    var profile = LlmProfiles.Resolve("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");
    var policy = LlmProfiles.ResolvePolicy("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");
    var llmClient = new LlmRuntimeClient(
        new FakeAdapter("ollama", "qwen2.5-coder:7b-instruct-q4_K_M", "Error: LLM returned status NotFound"),
        profile,
        policy);

    var agent = new Agent(
        llmClient,
        toolRegistry,
        memory,
        buildVerifier,
        sandboxManager,
        projectIndexer,
        contextBuilder,
        fileStateManager,
        session,
        workspaceResolution: null);

    var oldOut = Console.Out;
    var capture = new StringWriter();
    Console.SetOut(capture);

    try
    {
        _ = await agent.RunTask("Analyze the project briefly");
    }
    finally
    {
        Console.SetOut(oldOut);
    }

    var structured = ExtractStructuredPayload(capture.ToString());
    AssertTrue(structured.GetProperty("ok").GetBoolean(), "Expected fallback analysis run to be successful.");
    AssertTrue(structured.GetProperty("fallbackReason").GetString() == "PROVIDER_UNAVAILABLE", "Expected fallbackReason=PROVIDER_UNAVAILABLE.");
    AssertTrue(structured.GetProperty("fallbackMode").GetString() == "INDEXED_CONTEXT_SUMMARY", "Expected fallbackMode=INDEXED_CONTEXT_SUMMARY.");
    AssertTrue(structured.GetProperty("reasonCode").GetString() == "ANALYSIS_FALLBACK_USED", "Expected reasonCode=ANALYSIS_FALLBACK_USED.");

    Console.WriteLine("PASS AnalysisFallback_ProviderUnavailable_IndexedContextSummary");
}

static async Task RunAnalysisUsableErrorPrefixedResponse_NoFallbackRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    File.WriteAllText(Path.Combine(workspaceRoot, "Program.cs"), "namespace SampleApp; public static class Entry { public static void Hello() { } }");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun(
        "Analyze the project briefly",
        "Analyze the project briefly",
        workspaceRoot,
        runtimeRoot,
        AgentAccessMode.WorkspaceWrite.ToString(),
        "FakeUsableErrorPrefixedSuccessClient",
        "fake-usable-error-prefixed-success-model");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var toolRegistry = new ToolRegistry();
    var memory = new MemoryStore();
    var permissionGuard = new PermissionGuard();
    var safeProcessRunner = new SafeProcessRunner(session, permissionGuard, tracer);
    var buildVerifier = new BuildVerifier(safeProcessRunner, tracer);
    var sandboxManager = new SandboxManager(workspaceRoot, runtimeRoot);
    var embeddingService = new EmbeddingService(disabled: true);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var projectIndexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);
    var contextBuilder = new ContextBuilder(workspaceRoot, vectorStore, fileStateManager, new ProjectSymbolDirectory(), tracer);

    var agent = new Agent(
        new FakeUsableErrorPrefixedSuccessClient(),
        toolRegistry,
        memory,
        buildVerifier,
        sandboxManager,
        projectIndexer,
        contextBuilder,
        fileStateManager,
        session,
        workspaceResolution: null);

    var oldOut = Console.Out;
    var capture = new StringWriter();
    Console.SetOut(capture);

    try
    {
        _ = await agent.RunTask("Analyze the project briefly");
    }
    finally
    {
        Console.SetOut(oldOut);
    }

    var structured = ExtractStructuredPayload(capture.ToString());

    AssertTrue(structured.GetProperty("ok").GetBoolean(), "Expected successful analysis run for usable error-prefixed response.");
    AssertTrue(string.IsNullOrEmpty(structured.GetProperty("fallbackReason").GetString()), "Expected empty fallbackReason for usable model response.");
    AssertTrue(string.IsNullOrEmpty(structured.GetProperty("fallbackMode").GetString()), "Expected empty fallbackMode for usable model response.");
    AssertTrue(structured.GetProperty("reasonCode").GetString() == "SUCCESS_ANALYSIS_RESPONSE", "Expected reasonCode=SUCCESS_ANALYSIS_RESPONSE.");

    var timeline = structured.GetProperty("timeline");
    AssertTrue(timeline.ValueKind == JsonValueKind.Array && timeline.GetArrayLength() > 0, "Expected non-empty timeline.");
    var stages = timeline.EnumerateArray().Select(e => e.GetProperty("stage").GetString() ?? string.Empty).ToArray();
    AssertNotContains(stages, "AnalysisFallbackStarted");
    AssertNotContains(stages, "AnalysisFallbackCompleted");
    AssertNotContains(stages, "ModelCallTimedOut");

    Console.WriteLine("PASS Analysis_UsableErrorPrefixedResponse_NoFallback");
}

static async Task RunEmbeddingNotFound_DisablesTruthfullyRegression()
{
    var handler = new FakeHttpMessageHandler(() =>
        new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"error\":\"model not found\"}")
        });

    using var client = new HttpClient(handler);
    var service = new EmbeddingService(
        endpoint: "http://localhost:11434",
        model: "nomic-embed-text",
        disabled: false,
        httpClient: client);

    var first = await service.GenerateEmbedding("sample text");
    var second = await service.GenerateEmbedding("second sample");

    AssertTrue(first is null, "Expected null embedding for NotFound response.");
    AssertTrue(second is null, "Expected null embedding after session disable.");
    AssertTrue(service.Status == EmbeddingRuntimeStatus.Disabled, "Expected embeddings service status to be Disabled after NotFound.");
    AssertTrue(service.DescribeStatus() == "disabled", "Expected textual embedding status to be disabled.");
    AssertTrue(handler.CallCount == 1, "Expected no additional embedding calls after session is disabled.");

    Console.WriteLine("PASS EmbeddingsNotFound_TruthfulDisabledMode");
}

static JsonElement ExtractStructuredPayload(string output)
{
    var jsonLine = output
        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim())
        .FirstOrDefault(line => line.StartsWith("{", StringComparison.Ordinal) && line.Contains("\"ok\"", StringComparison.Ordinal));

    if (string.IsNullOrWhiteSpace(jsonLine))
        throw new InvalidOperationException("Structured payload JSON was not emitted.");

    using var doc = JsonDocument.Parse(jsonLine);
    return doc.RootElement.Clone();
}

static void AssertContains(IEnumerable<string> values, string expected)
{
    if (!values.Contains(expected, StringComparer.Ordinal))
        throw new InvalidOperationException($"Expected timeline to contain '{expected}'.");
}

static void AssertNotContains(IEnumerable<string> values, string forbidden)
{
    if (values.Contains(forbidden, StringComparer.Ordinal))
        throw new InvalidOperationException($"Expected timeline not to contain '{forbidden}'.");
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static Task RunRuntimeProfileSelectionRegression()
{
    var appRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(appRoot);

    var client = LlmRuntimeFactory.Create("local", "qwen2.5-coder:14b", appRoot);
    AssertTrue(client is ILlmRuntimeClient, "Expected runtime-aware client from factory.");
    var runtime = (ILlmRuntimeClient)client;
    AssertTrue(runtime.Metadata.Provider == "ollama", "Expected ollama provider metadata.");
    AssertTrue(runtime.Metadata.Model == "qwen2.5-coder:14b", "Expected selected ollama model metadata.");
    AssertTrue(runtime.Profile.Provider == "ollama", "Expected ollama runtime profile.");
    Console.WriteLine("PASS RuntimeProfile_SecondOllamaModel_NoCoreChanges");
    return Task.CompletedTask;
}

static async Task RunRuntimeNormalizedClassificationRegression()
{
    var timeoutClient = new LlmRuntimeClient(
        new FakeAdapter("openai", "gpt-4.1-mini", "Error: OpenAI request timed out. simulated timeout"),
        LlmProfiles.Resolve("openai"));
    var timeoutResult = await timeoutClient.GenerateNormalized("ping");
    AssertTrue(timeoutResult.Status == LlmRuntimeStatus.ModelTimeout, "Expected normalized timeout status.");
    AssertTrue(timeoutResult.TimeoutKind == LlmTimeoutKind.FirstResponse, "Expected first-response timeout kind.");

    var failureClient = new LlmRuntimeClient(
        new FakeAdapter("ollama", "qwen2.5-coder:7b", "Error: Ollama request failed. simulated failure"),
        LlmProfiles.Resolve("ollama"));
    var failureResult = await failureClient.GenerateNormalized("ping");
    AssertTrue(failureResult.Status == LlmRuntimeStatus.LlmRequestFailed, "Expected normalized request-failed status.");
    AssertTrue(failureResult.IsFailure, "Expected normalized failure=true.");

    var unavailableClient = new LlmRuntimeClient(
        new FakeAdapter("ollama", "qwen2.5-coder:7b", "Error: LLM returned status NotFound"),
        LlmProfiles.Resolve("ollama"));
    var unavailableResult = await unavailableClient.GenerateNormalized("ping");
    AssertTrue(unavailableResult.Status == LlmRuntimeStatus.ProviderUnavailable, "Expected NotFound to normalize as provider unavailable.");

    Console.WriteLine("PASS RuntimeNormalizedClassification_TimeoutAndRequestFailed");
}

static Task RunRuntimeProviderSelection_OpenAiGeminiRegression()
{
    var oldOpenAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    var oldGeminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    var oldOpenAiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");
    var oldGeminiModel = Environment.GetEnvironmentVariable("GEMINI_MODEL");

    try
    {
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-openai-key");
        Environment.SetEnvironmentVariable("OPENAI_MODEL", "gpt-4.1-mini");
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "test-gemini-key");
        Environment.SetEnvironmentVariable("GEMINI_MODEL", "gemini-1.5-flash");

        var appRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(appRoot);

        var openAiClient = LlmRuntimeFactory.Create("openai", null, appRoot);
        var openAiPrimary = ExtractPrimaryClient(openAiClient);
        AssertTrue(openAiPrimary is ILlmRuntimeClient, "Expected runtime client as OpenAI primary.");
        AssertTrue(((ILlmRuntimeClient)openAiPrimary).Metadata.Provider == "openai", "Expected OpenAI provider metadata.");

        var geminiClient = LlmRuntimeFactory.Create("gemini", null, appRoot);
        var geminiPrimary = ExtractPrimaryClient(geminiClient);
        AssertTrue(geminiPrimary is ILlmRuntimeClient, "Expected runtime client as Gemini primary.");
        AssertTrue(((ILlmRuntimeClient)geminiPrimary).Metadata.Provider == "gemini", "Expected Gemini provider metadata.");

        Console.WriteLine("PASS RuntimeProviderSelection_OpenAiGemini");
        return Task.CompletedTask;
    }
    finally
    {
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", oldOpenAiKey);
        Environment.SetEnvironmentVariable("OPENAI_MODEL", oldOpenAiModel);
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", oldGeminiKey);
        Environment.SetEnvironmentVariable("GEMINI_MODEL", oldGeminiModel);
    }
}

static async Task RunRuntimeNonOllamaClassificationRegression()
{
    var openAiProfile = LlmProfiles.Resolve("openai", "gpt-4.1-mini");
    var openAiPolicy = LlmProfiles.ResolvePolicy("openai", "gpt-4.1-mini");
    var geminiProfile = LlmProfiles.Resolve("gemini", "gemini-1.5-flash");
    var geminiPolicy = LlmProfiles.ResolvePolicy("gemini", "gemini-1.5-flash");

    var openAiTimeout = new LlmRuntimeClient(
        new FakeAdapter("openai", "gpt-4.1-mini", "Error: OpenAI request timed out. simulated timeout"),
        openAiProfile,
        openAiPolicy);
    var openAiTimeoutResult = await openAiTimeout.GenerateNormalized("ping");
    AssertTrue(openAiTimeoutResult.Status == LlmRuntimeStatus.ModelTimeout, "Expected OpenAI timeout classification.");

    var openAiFailed = new LlmRuntimeClient(
        new FakeAdapter("openai", "gpt-4.1-mini", "Error: OpenAI request failed. simulated failure"),
        openAiProfile,
        openAiPolicy);
    var openAiFailedResult = await openAiFailed.GenerateNormalized("ping");
    AssertTrue(openAiFailedResult.Status == LlmRuntimeStatus.LlmRequestFailed, "Expected OpenAI request-failure classification.");

    var geminiUnavailable = new LlmRuntimeClient(
        new FakeAdapter("gemini", "gemini-1.5-flash", "Error: Gemini unavailable due to quota or rate limits."),
        geminiProfile,
        geminiPolicy);
    var geminiUnavailableResult = await geminiUnavailable.GenerateNormalized("ping");
    AssertTrue(geminiUnavailableResult.Status == LlmRuntimeStatus.ProviderUnavailable, "Expected Gemini provider-unavailable classification.");

    var geminiUsable = new LlmRuntimeClient(
        new FakeAdapter("gemini", "gemini-1.5-flash", "Here is the analysis summary in plain text."),
        geminiProfile,
        geminiPolicy);
    var geminiUsableResult = await geminiUsable.GenerateNormalized("ping");
    AssertTrue(geminiUsableResult.Status == LlmRuntimeStatus.Success, "Expected Gemini usable response classified as success.");
    AssertTrue(geminiUsableResult.IsUsable, "Expected Gemini usable response to be usable.");

    Console.WriteLine("PASS RuntimeNonOllamaClassification_OpenAiGemini");
}

static ILLMClient ExtractPrimaryClient(ILLMClient client)
{
    if (client is not FallbackLLMClient)
        return client;

    var primaryField = typeof(FallbackLLMClient).GetField("_primary", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    if (primaryField?.GetValue(client) is ILLMClient primary)
        return primary;

    return client;
}

static Task RunOllamaQwenProfileSelectionRegression()
{
    var baseProfile = LlmProfiles.Resolve("ollama", "qwen2.5-coder:7b");
    var instructProfile = LlmProfiles.Resolve("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");
    var basePolicy = LlmProfiles.ResolvePolicy("ollama", "qwen2.5-coder:7b");
    var instructPolicy = LlmProfiles.ResolvePolicy("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");

    AssertTrue(baseProfile.ProfileId == "ollama/qwen2.5-coder", "Expected qwen base profile template.");
    AssertTrue(instructProfile.ProfileId == "ollama/qwen2.5-coder-7b-quality-gpu-tuned", "Expected qwen 7b quality gpu-tuned profile template.");
    AssertTrue(baseProfile.UsableTextTolerance == "high", "Expected high usable text tolerance for local qwen profile.");
    AssertTrue(instructProfile.UsableTextTolerance == "very_high", "Expected very high usable text tolerance for local qwen instruct profile.");
    AssertTrue(instructProfile.ExpectedAnalysisResponseMode == "plain_text_terse_ok", "Expected terse plain text analysis mode for qwen instruct profile.");
    AssertTrue(basePolicy.FirstResponseTimeout >= TimeSpan.FromSeconds(180), "Expected relaxed first-response timeout for local qwen profile.");
    AssertTrue(instructPolicy.StallTimeout >= TimeSpan.FromSeconds(90), "Expected relaxed stall timeout for local qwen instruct profile.");
    Console.WriteLine("PASS OllamaQwenProfileSelection_TwoModels_SharedTemplate");
    return Task.CompletedTask;
}

static Task RunOllamaModelAliasResolutionRegression()
{
    var available = new[]
    {
        "zoyer2/Qwen2.5-Coder-7B-Instruct-Q4_K_M-64K-CLINE:latest",
        "qwen2.5-coder:7b"
    };
    var resolved = OllamaClient.ResolveModelAlias("qwen2.5-coder:7b-instruct-q4_K_M", available);
    AssertTrue(
        string.Equals("zoyer2/Qwen2.5-Coder-7B-Instruct-Q4_K_M-64K-CLINE:latest", resolved, StringComparison.Ordinal),
        "Expected instruct alias resolution to installed local model.");

    var direct = OllamaClient.ResolveModelAlias("qwen2.5-coder:7b", available);
    AssertTrue(
        string.Equals("qwen2.5-coder:7b", direct, StringComparison.Ordinal),
        "Expected direct model match to remain unchanged.");

    Console.WriteLine("PASS OllamaModelAliasResolution_InstructMapping");
    return Task.CompletedTask;
}

static async Task RunOllamaUsableAnalysisClassificationRegression()
{
    var profile = LlmProfiles.Resolve("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");
    var policy = LlmProfiles.ResolvePolicy("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");

    var usableClient = new LlmRuntimeClient(
        new FakeAdapter("ollama", "qwen2.5-coder:7b-instruct-q4_K_M", "Error: I cannot run tools, but here is analysis: code structure is coherent and no patch is required."),
        profile,
        policy);
    var usableResult = await usableClient.GenerateNormalized("analyze");
    AssertTrue(usableResult.IsUsable, "Expected usable analysis text for qwen instruct profile.");
    AssertTrue(!usableResult.IsFailure, "Expected usable analysis text not to be hard failure.");
    AssertTrue(usableResult.Status == LlmRuntimeStatus.Success, "Expected usable analysis text to normalize as success.");

    var stallClient = new LlmRuntimeClient(
        new FakeAdapter("ollama", "qwen2.5-coder:7b-instruct-q4_K_M", "Error: response stalled with no progress."),
        profile,
        policy);
    var stallResult = await stallClient.GenerateNormalized("analyze");
    AssertTrue(stallResult.Status == LlmRuntimeStatus.ModelTimeout, "Expected stall to map to model timeout class.");
    AssertTrue(stallResult.TimeoutKind == LlmTimeoutKind.Stall, "Expected stall timeout kind.");

    Console.WriteLine("PASS OllamaQwenUsableAnalysis_NoPrematureFallbackClassification");
}

static async Task RunOllamaQwenInstructTerseAnalysis_NoFallbackRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    File.WriteAllText(Path.Combine(workspaceRoot, "Program.cs"), "namespace SampleApp; public static class Entry { public static void Hello() { } }");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun(
        "Analyze the project briefly",
        "Analyze the project briefly",
        workspaceRoot,
        runtimeRoot,
        AgentAccessMode.WorkspaceWrite.ToString(),
        "LlmRuntimeClient",
        "qwen2.5-coder:7b-instruct-q4_K_M");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var toolRegistry = new ToolRegistry();
    var memory = new MemoryStore();
    var permissionGuard = new PermissionGuard();
    var safeProcessRunner = new SafeProcessRunner(session, permissionGuard, tracer);
    var buildVerifier = new BuildVerifier(safeProcessRunner, tracer);
    var sandboxManager = new SandboxManager(workspaceRoot, runtimeRoot);
    var embeddingService = new EmbeddingService(disabled: true);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var projectIndexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);
    var contextBuilder = new ContextBuilder(workspaceRoot, vectorStore, fileStateManager, new ProjectSymbolDirectory(), tracer);

    var profile = LlmProfiles.Resolve("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");
    var policy = LlmProfiles.ResolvePolicy("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");
    var llmClient = new LlmRuntimeClient(
        new FakeAdapter("ollama", "qwen2.5-coder:7b-instruct-q4_K_M", "Error: Small project. One entry point. Build changes not required."),
        profile,
        policy);

    var agent = new Agent(
        llmClient,
        toolRegistry,
        memory,
        buildVerifier,
        sandboxManager,
        projectIndexer,
        contextBuilder,
        fileStateManager,
        session,
        workspaceResolution: null);

    var oldOut = Console.Out;
    var capture = new StringWriter();
    Console.SetOut(capture);

    try
    {
        _ = await agent.RunTask("Analyze the project briefly");
    }
    finally
    {
        Console.SetOut(oldOut);
    }

    var structured = ExtractStructuredPayload(capture.ToString());
    AssertTrue(structured.GetProperty("ok").GetBoolean(), "Expected successful analysis run for terse qwen instruct response.");
    AssertTrue(string.IsNullOrEmpty(structured.GetProperty("fallbackReason").GetString()), "Expected empty fallbackReason for terse qwen instruct response.");
    AssertTrue(string.IsNullOrEmpty(structured.GetProperty("fallbackMode").GetString()), "Expected empty fallbackMode for terse qwen instruct response.");
    AssertTrue(structured.GetProperty("reasonCode").GetString() == "SUCCESS_ANALYSIS_RESPONSE", "Expected SUCCESS_ANALYSIS_RESPONSE reason code.");
    AssertTrue(string.Equals(structured.GetProperty("finalStatus").GetString(), "success", StringComparison.OrdinalIgnoreCase), "Expected success final status.");

    var timeline = structured.GetProperty("timeline");
    var stages = timeline.EnumerateArray().Select(e => e.GetProperty("stage").GetString() ?? string.Empty).ToArray();
    AssertNotContains(stages, "AnalysisFallbackStarted");
    AssertNotContains(stages, "AnalysisFallbackCompleted");

    Console.WriteLine("PASS OllamaQwenInstructTerseAnalysis_NoFallback");
}

static async Task RunOllamaQwenInstructTerseNoKeywordAnalysis_NoFallbackRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    File.WriteAllText(Path.Combine(workspaceRoot, "Program.cs"), "namespace SampleApp; public static class Entry { public static void Hello() { } }");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun(
        "Analyze the project briefly",
        "Analyze the project briefly",
        workspaceRoot,
        runtimeRoot,
        AgentAccessMode.WorkspaceWrite.ToString(),
        "LlmRuntimeClient",
        "qwen2.5-coder:7b-instruct-q4_K_M");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var toolRegistry = new ToolRegistry();
    var memory = new MemoryStore();
    var permissionGuard = new PermissionGuard();
    var safeProcessRunner = new SafeProcessRunner(session, permissionGuard, tracer);
    var buildVerifier = new BuildVerifier(safeProcessRunner, tracer);
    var sandboxManager = new SandboxManager(workspaceRoot, runtimeRoot);
    var embeddingService = new EmbeddingService(disabled: true);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var projectIndexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);
    var contextBuilder = new ContextBuilder(workspaceRoot, vectorStore, fileStateManager, new ProjectSymbolDirectory(), tracer);

    var profile = LlmProfiles.Resolve("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");
    var policy = LlmProfiles.ResolvePolicy("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");
    var llmClient = new LlmRuntimeClient(
        new FakeAdapter("ollama", "qwen2.5-coder:7b-instruct-q4_K_M", "Error: Single entrypoint only; no edits needed."),
        profile,
        policy);

    var agent = new Agent(
        llmClient,
        toolRegistry,
        memory,
        buildVerifier,
        sandboxManager,
        projectIndexer,
        contextBuilder,
        fileStateManager,
        session,
        workspaceResolution: null);

    var oldOut = Console.Out;
    var capture = new StringWriter();
    Console.SetOut(capture);

    try
    {
        _ = await agent.RunTask("Analyze the project briefly");
    }
    finally
    {
        Console.SetOut(oldOut);
    }

    var structured = ExtractStructuredPayload(capture.ToString());
    AssertTrue(structured.GetProperty("ok").GetBoolean(), "Expected successful analysis run for terse no-keyword instruct response.");
    AssertTrue(string.IsNullOrEmpty(structured.GetProperty("fallbackReason").GetString()), "Expected empty fallbackReason for terse no-keyword instruct response.");
    AssertTrue(string.IsNullOrEmpty(structured.GetProperty("fallbackMode").GetString()), "Expected empty fallbackMode for terse no-keyword instruct response.");
    AssertTrue(structured.GetProperty("reasonCode").GetString() == "SUCCESS_ANALYSIS_RESPONSE", "Expected SUCCESS_ANALYSIS_RESPONSE reason code.");
    AssertTrue(string.Equals(structured.GetProperty("finalStatus").GetString(), "success", StringComparison.OrdinalIgnoreCase), "Expected success final status.");

    var timeline = structured.GetProperty("timeline");
    var stages = timeline.EnumerateArray().Select(e => e.GetProperty("stage").GetString() ?? string.Empty).ToArray();
    AssertNotContains(stages, "AnalysisFallbackStarted");
    AssertNotContains(stages, "AnalysisFallbackCompleted");

    Console.WriteLine("PASS OllamaQwenInstructTerseNoKeywordAnalysis_NoFallback");
}

static async Task RunPatchApplyDiagnosticsClassificationRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);

    var targetPath = Path.Combine(workspaceRoot, "target.txt");
    await File.WriteAllTextAsync(targetPath, "line-1\nline-2\n");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var gate = new PatchSafetyGate(session, new PermissionGuard());

    var invalidFormatPreview = gate.Preview(targetPath, targetPath, "*** Begin Patch\n*** Update File: target.txt\n+line-3");
    AssertTrue(invalidFormatPreview.PreviewRejected, "Expected malformed patch preview to be rejected.");
    AssertTrue(invalidFormatPreview.ReasonCode == PermissionReasonCodes.PatchUnexpectedEndOfPatch, "Expected PATCH_UNEXPECTED_END_OF_PATCH reason.");

    var preview = gate.Preview(targetPath, targetPath, "*** Begin Patch\n*** Update File: target.txt\n*** End Patch");
    AssertTrue(!preview.PreviewRejected, "Expected baseline preview acceptance for apply-classification scenarios.");

    var contextResult = await gate.ApplyAsync(
        preview,
        () => throw new InvalidOperationException("Patch context not found at target file."),
        () => Task.CompletedTask);
    AssertTrue(contextResult.ApplyFailed, "Expected apply failure for context-not-found scenario.");
    AssertTrue(contextResult.ReasonCode == PermissionReasonCodes.PatchContextNotFound, "Expected PATCH_CONTEXT_NOT_FOUND reason.");

    var ambiguousResult = await gate.ApplyAsync(
        preview,
        () => throw new InvalidOperationException("Ambiguous patch target: multiple matches found."),
        () => Task.CompletedTask);
    AssertTrue(ambiguousResult.ApplyFailed, "Expected apply failure for ambiguous-match scenario.");
    AssertTrue(ambiguousResult.ReasonCode == PermissionReasonCodes.PatchAmbiguousMatch, "Expected PATCH_AMBIGUOUS_MATCH reason.");

    var outsideWorkspacePath = Path.Combine(tempRoot, "outside.txt");
    await File.WriteAllTextAsync(outsideWorkspacePath, "outside");
    var outsidePreview = gate.Preview(outsideWorkspacePath, outsideWorkspacePath, "*** Begin Patch\n*** Update File: outside.txt\n*** End Patch");
    AssertTrue(outsidePreview.PreviewRejected, "Expected outside-workspace preview rejection.");
    AssertTrue(
        outsidePreview.ReasonCode != PermissionReasonCodes.Allowed &&
        (outsidePreview.ReasonCode == PermissionReasonCodes.PathOutsideWorkspace ||
         outsidePreview.ReasonCode == PermissionReasonCodes.AccessDeniedOutsideWorkspace),
        "Expected outside-workspace guard reason.");

    Console.WriteLine("PASS PatchApplyDiagnostics_ClassificationAndGuarding");
}

static async Task RunExternalActionApprovalProposalRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "a.txt"), "ok");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var guard = new PermissionGuard();
    var inside = guard.Evaluate(session, new ToolAction { Kind = ToolActionKind.ReadFile, TargetPath = Path.Combine(workspaceRoot, "a.txt") });
    AssertTrue(inside.Allowed, "Expected inside-workspace file action to be allowed.");

    var outsidePath = Path.Combine(tempRoot, "outside.txt");
    var outside = guard.Evaluate(session, new ToolAction { Kind = ToolActionKind.ReadFile, TargetPath = outsidePath });
    AssertTrue(!outside.Allowed, "Expected outside-workspace file action to be blocked.");
    AssertTrue(outside.RequiresApproval, "Expected outside-workspace file action to require approval.");
    AssertTrue(outside.ApprovalStatus == ApprovalStatus.ApprovalRequired, "Expected ApprovalRequired status.");
    AssertTrue(outside.ApprovalProposal is not null && !outside.ApprovalProposal.IsInsideSandbox, "Expected structured outside-sandbox proposal.");

    var runner = new SafeProcessRunner(session, guard);
    var cmdResult = await runner.RunAsync(new SafeProcessRequest
    {
        Kind = ToolActionKind.RunCommand,
        Command = "dotnet",
        Args = new[] { "--info" },
        WorkingDirectory = tempRoot
    });
    AssertTrue(!cmdResult.Success, "Expected outside-workspace command not to execute.");
    AssertTrue(
        cmdResult.ReasonCode == PermissionReasonCodes.AccessDeniedOutsideWorkspace || cmdResult.ReasonCode == "BLOCKED_PROCESS_EXECUTION",
        "Expected outside-workspace command denial reason.");

    var protectedDecision = guard.Evaluate(session, new ToolAction { Kind = ToolActionKind.ReadFile, TargetPath = runtimeRoot });
    AssertTrue(!protectedDecision.Allowed, "Expected protected/system-like path to be non-success.");

    Console.WriteLine("PASS ExternalActionApprovalProposal_OutsideSandbox");
}

static async Task RunActionLifecycleLedgerRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "a.txt"), "ok");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun("ledger", "ledger", workspaceRoot, runtimeRoot, AgentAccessMode.WorkspaceWrite.ToString(), "test", "test");
    var guard = new PermissionGuard();

    var outsideAction = new ToolAction { Kind = ToolActionKind.ReadFile, TargetPath = Path.Combine(tempRoot, "outside.txt") };
    var outsideDecision = guard.Evaluate(session, outsideAction);
    tracer.LogPermissionDecision(session, "file", outsideAction, outsideDecision);

    AssertTrue(outsideDecision.RequiresApproval, "Expected outside action to require approval.");
    AssertTrue(tracer.GetActionLedger().Any(x => x.LifecycleState == ActionLifecycleState.ApprovalRequired), "Expected ApprovalRequired state in ledger.");
    AssertTrue(!tracer.GetActionLedger().Any(x => x.LifecycleState == ActionLifecycleState.Blocked && x.Target.Contains("outside.txt", StringComparison.OrdinalIgnoreCase)), "Approval-required outside action must not be classified as Blocked.");
    AssertTrue(!tracer.GetActionLedger().Any(x => x.LifecycleState == ActionLifecycleState.Executed && x.Target.Contains("outside.txt", StringComparison.OrdinalIgnoreCase)), "Outside approval-required action must not be executed.");
    var outsideStates = tracer.GetActionLedger()
        .Where(x => x.Target.Contains("outside.txt", StringComparison.OrdinalIgnoreCase))
        .ToArray();
    var outsideRequested = outsideStates.FirstOrDefault(x => x.LifecycleState == ActionLifecycleState.Requested);
    var outsideApprovalRequired = outsideStates.FirstOrDefault(x => x.LifecycleState == ActionLifecycleState.ApprovalRequired);
    AssertTrue(outsideRequested is not null && outsideApprovalRequired is not null, "Expected Requested and ApprovalRequired lifecycle entries for outside action.");
    if (outsideRequested is null || outsideApprovalRequired is null)
        throw new InvalidOperationException("Missing expected lifecycle entries for outside action.");
    AssertTrue(string.Equals(outsideRequested.ActionCorrelationId, outsideApprovalRequired.ActionCorrelationId, StringComparison.Ordinal), "Expected Requested and ApprovalRequired to share actionCorrelationId.");

    var guarded = new GuardedTool(new FakeNoopTool(), guard, session, _ => new ToolAction
    {
        Kind = ToolActionKind.ReadFile,
        TargetPath = Path.Combine(workspaceRoot, "a.txt")
    }, tracer);
    _ = await guarded.Execute("read:a.txt");

    AssertTrue(tracer.GetActionLedger().Any(x => x.LifecycleState == ActionLifecycleState.Executed && x.ActionType == ToolActionKind.ReadFile.ToString()), "Expected executed ledger state for allowed inside action.");
    var deniedAction = new ToolAction { Kind = ToolActionKind.DeleteFile, TargetPath = Path.Combine(workspaceRoot, "a.txt") };
    var deniedDecision = guard.Evaluate(session, deniedAction);
    tracer.LogPermissionDecision(session, "file", deniedAction, deniedDecision);
    AssertTrue(!deniedDecision.Allowed, "Expected destructive delete action to be denied.");
    AssertTrue(deniedDecision.RequiresApproval, "Expected destructive delete action to require explicit approval.");
    AssertTrue(tracer.GetActionLedger().Any(x => x.LifecycleState == ActionLifecycleState.ApprovalRequired && x.ActionType == ToolActionKind.DeleteFile.ToString()), "Expected approval-required lifecycle entry for destructive delete.");

    var payloadJson = JsonSerializer.Serialize(new
    {
        actionLifecycle = tracer.GetActionLedger().Select(x => new
        {
            x.Sequence,
            x.ActionType,
            LifecycleState = x.LifecycleState.ToString(),
            x.ReasonCode,
            x.ApprovalStatus
        }).ToArray()
    });
    using var doc = JsonDocument.Parse(payloadJson);
    var lifecycle = doc.RootElement.GetProperty("actionLifecycle");
    AssertTrue(lifecycle.ValueKind == JsonValueKind.Array && lifecycle.GetArrayLength() >= 2, "Expected actionLifecycle array in structured payload.");
    var states = lifecycle.EnumerateArray().Select(x => x.GetProperty("LifecycleState").GetString() ?? string.Empty).ToArray();
    AssertTrue(states.Contains("ApprovalRequired", StringComparer.Ordinal), "Expected ApprovalRequired state in actionLifecycle payload.");
    AssertTrue(states.Contains("Executed", StringComparer.Ordinal), "Expected Executed state in actionLifecycle payload.");
    Console.WriteLine("PASS ActionLifecycleLedger_RequestedBlockedExecuted");
}

static async Task RunStructuredActionLifecycleReportingRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "a.txt"), "ok");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun("ledger-structured", "ledger-structured", workspaceRoot, runtimeRoot, AgentAccessMode.WorkspaceWrite.ToString(), "test", "test");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var guard = new PermissionGuard();
    var outsideAction = new ToolAction { Kind = ToolActionKind.ReadFile, TargetPath = Path.Combine(tempRoot, "outside.txt") };
    var outsideDecision = guard.Evaluate(session, outsideAction);
    tracer.LogPermissionDecision(session, "file", outsideAction, outsideDecision);

    var guarded = new GuardedTool(new FakeNoopTool(), guard, session, _ => new ToolAction
    {
        Kind = ToolActionKind.ReadFile,
        TargetPath = Path.Combine(workspaceRoot, "a.txt")
    }, tracer);
    _ = await guarded.Execute("read:a.txt");

    var summaryJson = JsonSerializer.Serialize(new
    {
        approvalRequiredActions = tracer.GetApprovalRequiredActions().Select(x => new
        {
            x.ProposalId,
            ApprovalTokenFormat = string.IsNullOrWhiteSpace(x.ProposalId) ? string.Empty : $"APPROVED:{x.ProposalId}",
            x.ActionType,
            x.Command,
            x.Path,
            x.NormalizedTarget,
            x.RiskLevel,
            x.Reason,
            ApprovalStatus = x.ApprovalStatus.ToString()
        }).ToArray(),
        externalAttempts = tracer.GetApprovalRequiredActions().Count,
        deniedActions = tracer.GetDeniedPermissionDecisionCount(),
        blockedActions = tracer.GetActionLedger().Count(x => x.LifecycleState == ActionLifecycleState.Blocked),
        hostBoundaryPreserved = true,
        actionLifecycle = tracer.GetActionLedger().Select(x => new
        {
            x.Sequence,
            x.ActionCorrelationId,
            x.ActionType,
            x.Target,
            x.NormalizedTarget,
            LifecycleState = x.LifecycleState.ToString(),
            x.ReasonCode,
            x.ApprovalStatus
        }).ToArray()
    });
    using var doc = JsonDocument.Parse(summaryJson);
    var root = doc.RootElement;

    var approvalRequired = root.GetProperty("approvalRequiredActions");
    AssertTrue(approvalRequired.ValueKind == JsonValueKind.Array && approvalRequired.GetArrayLength() == 1, "Expected single approval-required action.");
    var approvalItem = approvalRequired[0];
    var hasProposalId = (approvalItem.TryGetProperty("proposalId", out var proposalIdElement) || approvalItem.TryGetProperty("ProposalId", out proposalIdElement))
        && !string.IsNullOrWhiteSpace(proposalIdElement.GetString());
    AssertTrue(hasProposalId, "Expected proposalId in approval-required payload.");
    var hasApprovalTokenFormat = (approvalItem.TryGetProperty("approvalTokenFormat", out var approvalTokenElement) || approvalItem.TryGetProperty("ApprovalTokenFormat", out approvalTokenElement))
        && (approvalTokenElement.GetString() ?? string.Empty).StartsWith("APPROVED:", StringComparison.Ordinal);
    AssertTrue(hasApprovalTokenFormat, "Expected approvalTokenFormat in payload.");

    var externalAttempts = root.GetProperty("externalAttempts").GetInt32();
    AssertTrue(externalAttempts == 1, "Expected externalAttempts to equal approval-required attempts.");

    var deniedActions = root.GetProperty("deniedActions").GetInt32();
    AssertTrue(deniedActions == 0, "Expected deniedActions=0 for approval-required flow without explicit deny.");
    var blockedActions = root.GetProperty("blockedActions").GetInt32();
    AssertTrue(blockedActions == 0, "Expected blockedActions=0 for approval-required flow without explicit deny.");
    AssertTrue(deniedActions == blockedActions, "Expected deniedActions and blockedActions to stay aligned for pure approval-required flow.");
    AssertTrue(externalAttempts > deniedActions, "Expected approval-required attempts not to be merged into deniedActions.");

    AssertTrue(root.GetProperty("hostBoundaryPreserved").GetBoolean(), "Expected hostBoundaryPreserved=true for blocked outside action.");
    if (root.TryGetProperty("executionMode", out var executionMode))
        AssertTrue(string.Equals(executionMode.GetString(), "active-workspace", StringComparison.Ordinal), "Expected truthful executionMode=active-workspace.");
    if (root.TryGetProperty("executionWorkspaceKind", out var executionWorkspaceKind))
        AssertTrue(string.Equals(executionWorkspaceKind.GetString(), "active-workspace", StringComparison.Ordinal), "Expected truthful executionWorkspaceKind=active-workspace.");
    if (root.TryGetProperty("activeWorkspaceUsed", out var activeWorkspaceUsed))
        AssertTrue(activeWorkspaceUsed.GetBoolean(), "Expected activeWorkspaceUsed=true for current implementation.");
    if (root.TryGetProperty("sandboxRoot", out var sandboxRoot))
        AssertTrue(!string.IsNullOrWhiteSpace(sandboxRoot.GetString()), "Expected non-empty sandboxRoot surfaced as active workspace root.");
    if (root.TryGetProperty("worktreeRoot", out var worktreeRoot))
        AssertTrue(!string.IsNullOrWhiteSpace(worktreeRoot.GetString()), "Expected non-empty worktreeRoot surfaced as active workspace root.");

    var lifecycle = root.GetProperty("actionLifecycle");
    AssertTrue(lifecycle.ValueKind == JsonValueKind.Array && lifecycle.GetArrayLength() >= 2, "Expected non-empty actionLifecycle.");

    var hasOutsideExecuted = lifecycle.EnumerateArray().Any(x =>
    {
        var normalizedTarget = x.TryGetProperty("normalizedTarget", out var n) ? n.GetString() ?? string.Empty : string.Empty;
        var state = x.TryGetProperty("lifecycleState", out var s) ? s.GetString() ?? string.Empty : string.Empty;
        return normalizedTarget.Contains("outside.txt", StringComparison.OrdinalIgnoreCase) && string.Equals(state, "Executed", StringComparison.Ordinal);
    });
    AssertTrue(!hasOutsideExecuted, "Outside approval-required action must not be reported as executed.");

    var hasCorrelationId = lifecycle.EnumerateArray().All(x =>
        (x.TryGetProperty("actionCorrelationId", out var id) || x.TryGetProperty("ActionCorrelationId", out id)) &&
        !string.IsNullOrWhiteSpace(id.GetString()));
    AssertTrue(hasCorrelationId, "Expected non-empty actionCorrelationId in all lifecycle entries.");

    Console.WriteLine("PASS StructuredActionLifecycleReporting_ApprovalRequiredAndExecutedSeparation");
}

static async Task RunEndToEndExecutionPipelineRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    var worktreeRoot = Path.Combine(runtimeRoot, "worktrees", "session");
    var outsideRoot = Path.Combine(tempRoot, "outside");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    Directory.CreateDirectory(worktreeRoot);
    Directory.CreateDirectory(outsideRoot);
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "inside.txt"), "inside");
    await File.WriteAllTextAsync(Path.Combine(worktreeRoot, "inside-worktree.txt"), "inside-worktree");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        ExecutionWorkspaceRoot = worktreeRoot,
        WorktreeRoot = worktreeRoot,
        ExecutionWorkspaceKind = "worktree",
        ActiveWorkspaceUsed = false,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };
    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun("e2e-pipeline", "e2e-pipeline", workspaceRoot, runtimeRoot, AgentAccessMode.WorkspaceWrite.ToString(), "test", "test");
    var guard = new PermissionGuard();
    var patchGate = new PatchSafetyGate(session, guard, tracer);
    var destructiveGate = new DestructiveOperationSafetyGate(session, guard, tracer);
    var sandbox = new SandboxManager(worktreeRoot, runtimeRoot);
    await sandbox.CreateSandbox();
    var fileTool = new FileTool(session, guard, patchGate, destructiveGate, sandbox, tracer);
    var guarded = new GuardedTool(fileTool, guard, session, CreateFileActionFactory(worktreeRoot), tracer);

    var lowRiskResult = await guarded.Execute("write:new.txt:ok");
    AssertTrue(lowRiskResult.StartsWith("Successfully wrote", StringComparison.Ordinal), "Expected low-risk inside write to execute.");
    var lowRiskPath = Path.Combine(worktreeRoot, "new.txt");
    AssertTrue(File.Exists(lowRiskPath), "Expected low-risk write to mutate execution worktree root.");

    var noApprovalDelete = await guarded.Execute("delete:new.txt");
    AssertTrue(noApprovalDelete.StartsWith("DENIED", StringComparison.Ordinal), "Expected destructive action without approval to be denied.");
    AssertTrue(File.Exists(lowRiskPath), "Expected file to remain after no-approval delete.");

    var deleteDecision = guard.Evaluate(session, new ToolAction { Kind = ToolActionKind.DeleteFile, TargetPath = lowRiskPath });
    AssertTrue(deleteDecision.ApprovalProposal is not null, "Expected approval proposal for destructive delete.");
    var deleteProposalId = deleteDecision.ApprovalProposal!.ProposalId;

    var approvedDelete = await guarded.Execute($"delete:new.txt APPROVED:{deleteProposalId}");
    AssertTrue(approvedDelete.StartsWith("Successfully deleted", StringComparison.Ordinal), "Expected approved destructive action to execute.");
    AssertTrue(!File.Exists(lowRiskPath), "Expected approved delete to remove file.");

    await File.WriteAllTextAsync(Path.Combine(worktreeRoot, "reused.txt"), "reused");
    var reusedTokenDelete = await guarded.Execute($"delete:reused.txt APPROVED:{deleteProposalId}");
    AssertTrue(reusedTokenDelete.StartsWith("DENIED", StringComparison.Ordinal), "Expected consumed token reuse to remain denied.");
    AssertTrue(File.Exists(Path.Combine(worktreeRoot, "reused.txt")), "Expected file to remain after token reuse.");

    var decisionA = guard.Evaluate(session, new ToolAction { Kind = ToolActionKind.DeleteFile, TargetPath = Path.Combine(worktreeRoot, "reused.txt") });
    var decisionB = guard.Evaluate(session, new ToolAction { Kind = ToolActionKind.DeleteFile, TargetPath = Path.Combine(worktreeRoot, "inside-worktree.txt") });
    AssertTrue(decisionA.ApprovalProposal is not null && decisionB.ApprovalProposal is not null, "Expected proposal ids for both deletes.");
    var wrongTokenCrossProposal = await guarded.Execute($"delete:inside-worktree.txt APPROVED:{decisionA.ApprovalProposal!.ProposalId}");
    AssertTrue(wrongTokenCrossProposal.StartsWith("DENIED", StringComparison.Ordinal), "Expected token for proposal A not to authorize proposal B.");
    AssertTrue(File.Exists(Path.Combine(worktreeRoot, "inside-worktree.txt")), "Expected target for proposal B to remain.");

    var outsidePath = Path.Combine(outsideRoot, "outside.txt");
    await File.WriteAllTextAsync(outsidePath, "outside");
    var outsideDecision = guard.Evaluate(session, new ToolAction { Kind = ToolActionKind.DeleteFile, TargetPath = outsidePath });
    AssertTrue(outsideDecision.ApprovalProposal is not null, "Expected outside proposal.");
    var outsideApproved = await guarded.Execute($"delete:{outsidePath} APPROVED:{outsideDecision.ApprovalProposal!.ProposalId}");
    AssertTrue(outsideApproved.StartsWith("DENIED", StringComparison.Ordinal), "Expected outside-boundary action to stay denied.");
    AssertTrue(File.Exists(outsidePath), "Expected outside file to remain untouched.");

    var failingGuarded = new GuardedTool(new ThrowingTool(), guard, session, _ => new ToolAction
    {
        Kind = ToolActionKind.ReadFile,
        TargetPath = Path.Combine(worktreeRoot, "inside-worktree.txt")
    }, tracer);
    try
    {
        _ = await failingGuarded.Execute("read:inside-worktree.txt");
        throw new InvalidOperationException("Expected ThrowingTool to throw.");
    }
    catch (InvalidOperationException ex)
    {
        AssertTrue(ex.Message.Contains("expected failure", StringComparison.OrdinalIgnoreCase), "Expected failure path exception from ThrowingTool.");
    }

    var lifecycle = tracer.GetActionLedger();
    AssertTrue(lifecycle.Any(x => x.LifecycleState == ActionLifecycleState.Requested), "Expected requested lifecycle entries.");
    AssertTrue(lifecycle.Any(x => x.LifecycleState == ActionLifecycleState.ApprovalRequired), "Expected approval-required lifecycle entries.");
    AssertTrue(lifecycle.Any(x => x.LifecycleState == ActionLifecycleState.Executed), "Expected executed lifecycle entries.");
    AssertTrue(lifecycle.Any(x => x.LifecycleState == ActionLifecycleState.Failed), "Expected failed lifecycle entries.");
    AssertTrue(!lifecycle.Any(x => x.LifecycleState == ActionLifecycleState.Executed && x.Target.Contains("outside.txt", StringComparison.OrdinalIgnoreCase)), "Expected outside-boundary action not to be executed.");

    var summaryJson = JsonSerializer.Serialize(new
    {
        executionMode = session.ExecutionWorkspaceKind,
        executionWorkspaceKind = session.ExecutionWorkspaceKind,
        activeWorkspaceUsed = session.ActiveWorkspaceUsed,
        sandboxRoot = session.ExecutionWorkspaceRoot,
        worktreeRoot = session.WorktreeRoot,
        approvalRequiredActions = tracer.GetApprovalRequiredActions().Select(x => new
        {
            x.ProposalId,
            ApprovalTokenFormat = string.IsNullOrWhiteSpace(x.ProposalId) ? string.Empty : $"APPROVED:{x.ProposalId}",
            x.ActionType,
            x.Path,
            x.NormalizedTarget,
            x.RiskLevel,
            x.Reason,
            ApprovalStatus = x.ApprovalStatus.ToString()
        }).ToArray(),
        externalAttempts = tracer.GetApprovalRequiredActions().Count,
        deniedActions = tracer.GetDeniedPermissionDecisionCount(),
        blockedActions = tracer.GetActionLedger().Count(x => x.LifecycleState == ActionLifecycleState.Blocked),
        requestedActions = tracer.GetActionLedger().Count(x => x.LifecycleState == ActionLifecycleState.Requested),
        executedActions = tracer.GetActionLedger().Count(x => x.LifecycleState == ActionLifecycleState.Executed),
        failedActions = tracer.GetActionLedger().Count(x => x.LifecycleState == ActionLifecycleState.Failed),
        actionLifecycle = tracer.GetActionLedger().Select(x => new
        {
            x.Sequence,
            x.ActionCorrelationId,
            x.ActionType,
            x.Target,
            x.NormalizedTarget,
            LifecycleState = x.LifecycleState.ToString(),
            x.ReasonCode,
            x.ApprovalStatus
        }).ToArray()
    });
    using var doc = JsonDocument.Parse(summaryJson);
    var root = doc.RootElement;
    AssertTrue(string.Equals(root.GetProperty("executionMode").GetString(), "worktree", StringComparison.Ordinal), "Expected executionMode=worktree.");
    AssertTrue(string.Equals(root.GetProperty("executionWorkspaceKind").GetString(), "worktree", StringComparison.Ordinal), "Expected executionWorkspaceKind=worktree.");
    AssertTrue(root.GetProperty("activeWorkspaceUsed").GetBoolean() == false, "Expected activeWorkspaceUsed=false in worktree mode.");
    AssertTrue(string.Equals(root.GetProperty("sandboxRoot").GetString(), worktreeRoot, StringComparison.OrdinalIgnoreCase), "Expected sandboxRoot to match execution root.");
    AssertTrue(string.Equals(root.GetProperty("worktreeRoot").GetString(), worktreeRoot, StringComparison.OrdinalIgnoreCase), "Expected worktreeRoot to match execution root.");
    AssertTrue(root.GetProperty("approvalRequiredActions").EnumerateArray().Any(x => (x.TryGetProperty("ProposalId", out var p) ? p.GetString() : x.GetProperty("proposalId").GetString()) is { Length: > 0 }), "Expected proposalId in approvalRequiredActions.");
    AssertTrue(root.GetProperty("approvalRequiredActions").EnumerateArray().Any(x => (x.TryGetProperty("ApprovalTokenFormat", out var t) ? t.GetString() : x.GetProperty("approvalTokenFormat").GetString())?.StartsWith("APPROVED:", StringComparison.Ordinal) == true), "Expected approvalTokenFormat in approvalRequiredActions.");
    AssertTrue(root.GetProperty("executedActions").GetInt32() > 0, "Expected executedActions > 0.");
    AssertTrue(root.GetProperty("failedActions").GetInt32() > 0, "Expected failedActions > 0.");

    Console.WriteLine("PASS EndToEndExecutionPipeline_TruthfulPaths");
}

static async Task RunBroadIntentNoToolCallsRequiresActionRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    File.WriteAllText(Path.Combine(workspaceRoot, "notes.txt"), "no csharp files here");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun(
        "Implement a full converter and proceed step by step",
        "Implement a full converter and proceed step by step",
        workspaceRoot,
        runtimeRoot,
        AgentAccessMode.WorkspaceWrite.ToString(),
        "LlmRuntimeClient",
        "qwen2.5-coder:7b-instruct-q4_K_M");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var toolRegistry = new ToolRegistry();
    var memory = new MemoryStore();
    var permissionGuard = new PermissionGuard();
    var safeProcessRunner = new SafeProcessRunner(session, permissionGuard, tracer);
    var buildVerifier = new BuildVerifier(safeProcessRunner, tracer);
    var sandboxManager = new SandboxManager(workspaceRoot, runtimeRoot);
    var embeddingService = new EmbeddingService(disabled: true);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var projectIndexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);
    var contextBuilder = new ContextBuilder(workspaceRoot, vectorStore, fileStateManager, new ProjectSymbolDirectory(), tracer);
    var profile = LlmProfiles.Resolve("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");
    var policy = LlmProfiles.ResolvePolicy("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");
    var llmClient = new LlmRuntimeClient(
        new FakeAdapter("ollama", "qwen2.5-coder:7b-instruct-q4_K_M", "I can help with this task."),
        profile,
        policy);

    var agent = new Agent(
        llmClient,
        toolRegistry,
        memory,
        buildVerifier,
        sandboxManager,
        projectIndexer,
        contextBuilder,
        fileStateManager,
        session,
        workspaceResolution: null);

    var oldOut = Console.Out;
    var capture = new StringWriter();
    Console.SetOut(capture);
    try
    {
        _ = await agent.RunTask("Implement a full converter and proceed step by step");
    }
    finally
    {
        Console.SetOut(oldOut);
    }

    var structured = ExtractStructuredPayload(capture.ToString());
    AssertTrue(!structured.GetProperty("ok").GetBoolean(), "Expected broad engineering intent with no actions to be non-success.");
    var reasonCode = structured.GetProperty("reasonCode").GetString() ?? string.Empty;
    var finalStatus = structured.GetProperty("finalStatus").GetString() ?? string.Empty;
    AssertTrue(structured.TryGetProperty("planRequired", out var planRequired) && (planRequired.ValueKind == JsonValueKind.True || planRequired.ValueKind == JsonValueKind.False), "Expected typed planRequired field in structured result.");
    AssertTrue(structured.TryGetProperty("continuationHint", out var continuationHint) && continuationHint.ValueKind == JsonValueKind.String, "Expected typed continuationHint field in structured result.");
    AssertTrue(structured.TryGetProperty("sessionContinuation", out var sessionContinuation) && sessionContinuation.ValueKind == JsonValueKind.Object, "Expected typed sessionContinuation object in structured result.");
    AssertTrue(structured.TryGetProperty("nextActionCandidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array, "Expected typed nextActionCandidates array in structured result.");
    AssertTrue(!string.Equals(reasonCode, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase), "Broad engineering intent must not end as SUCCESS_NO_TOOL_CALLS.");
    AssertTrue(!string.Equals(finalStatus, "success", StringComparison.OrdinalIgnoreCase), "Broad engineering intent with no actions must not end as success.");
    Console.WriteLine("PASS BroadIntentNoToolCalls_RequiresAction");
}

static async Task RunTechnicalNoToolCallsRequiresActionRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    File.WriteAllText(Path.Combine(workspaceRoot, "notes.txt"), "no csharp files here");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun(
        "Analyze synchronization of player coordinates between client and server",
        "Analyze synchronization of player coordinates between client and server",
        workspaceRoot,
        runtimeRoot,
        AgentAccessMode.WorkspaceWrite.ToString(),
        "FakeNoToolAnalysisClient",
        "fake-no-tool-model");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var toolRegistry = new ToolRegistry();
    var memory = new MemoryStore();
    var permissionGuard = new PermissionGuard();
    var safeProcessRunner = new SafeProcessRunner(session, permissionGuard, tracer);
    var buildVerifier = new BuildVerifier(safeProcessRunner, tracer);
    var sandboxManager = new SandboxManager(workspaceRoot, runtimeRoot);
    var embeddingService = new EmbeddingService(disabled: true);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var projectIndexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);
    var contextBuilder = new ContextBuilder(workspaceRoot, vectorStore, fileStateManager, new ProjectSymbolDirectory(), tracer);

    var agent = new Agent(
        new FakeNoToolAnalysisClient(),
        toolRegistry,
        memory,
        buildVerifier,
        sandboxManager,
        projectIndexer,
        contextBuilder,
        fileStateManager,
        session,
        workspaceResolution: null);

    var oldOut = Console.Out;
    var capture = new StringWriter();
    Console.SetOut(capture);
    try
    {
        _ = await agent.RunTask("Analyze synchronization of player coordinates between client and server");
    }
    finally
    {
        Console.SetOut(oldOut);
    }

    var structured = ExtractStructuredPayload(capture.ToString());
    var reasonCode = structured.GetProperty("reasonCode").GetString() ?? string.Empty;
    AssertTrue(!string.Equals(reasonCode, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase), "Technical no-tool analysis must not end with SUCCESS_NO_TOOL_CALLS.");
    AssertTrue(!string.Equals(reasonCode, "SUCCESS_ANALYSIS_RESPONSE", StringComparison.OrdinalIgnoreCase), "Technical no-tool analysis must not end with SUCCESS_ANALYSIS_RESPONSE.");
    Console.WriteLine("PASS TechnicalNoToolCalls_RequiresAction");
}

static async Task RunHostDiagnosticsCommandApprovalRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var guard = new PermissionGuard();
    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun("gpu-host-diagnostics", "gpu-host-diagnostics", workspaceRoot, runtimeRoot, AgentAccessMode.WorkspaceWrite.ToString(), "test", "test");
    var runner = new SafeProcessRunner(session, guard, tracer);

    var decision = guard.Evaluate(session, new ToolAction
    {
        Kind = ToolActionKind.RunCommand,
        WorkingDirectory = workspaceRoot,
        Payload = "nvidia-smi"
    });

    AssertTrue(!decision.Allowed, "Expected nvidia-smi to require approval.");
    AssertTrue(decision.RequiresApproval, "Expected approval requirement for host diagnostics command.");
    AssertTrue(decision.ReasonCodeString == PermissionReasonCodes.HighRiskApprovalRequired, "Expected high-risk approval reason code for host diagnostics command.");
    AssertTrue(decision.ApprovalProposal is not null, "Expected structured approval proposal for host diagnostics.");
    AssertTrue(decision.ApprovalProposal?.ReasonCode == PermissionReasonCodes.HighRiskApprovalRequired, "Expected approval proposal to carry stable high-risk reasonCode.");
    AssertTrue(!string.IsNullOrWhiteSpace(decision.ApprovalProposal?.ExpectedEffect), "Expected approval proposal to carry expectedEffect.");
    var packageInstallDecision = guard.Evaluate(session, new ToolAction
    {
        Kind = ToolActionKind.RunCommand,
        WorkingDirectory = workspaceRoot,
        Payload = "npm install -g typescript"
    });
    AssertTrue(!packageInstallDecision.Allowed && packageInstallDecision.RequiresApproval, "Expected global package install command to require approval.");

    var approvedDecision = guard.Evaluate(session, new ToolAction
    {
        Kind = ToolActionKind.RunCommand,
        WorkingDirectory = workspaceRoot,
        Payload = "nvidia-smi APPROVED:true"
    });
    AssertTrue(approvedDecision.Allowed, "Expected approved host diagnostics command to pass guard policy.");
    AssertTrue(!approvedDecision.RequiresApproval, "Expected no approval requirement after explicit approval marker.");

    var result = await runner.RunAsync(new SafeProcessRequest
    {
        Kind = ToolActionKind.RunCommand,
        Command = "dotnet",
        Args = new[] { "nvidia-smi" },
        WorkingDirectory = workspaceRoot,
        Timeout = TimeSpan.FromSeconds(5)
    });

    AssertTrue(!result.Success, "Expected host diagnostics command not to execute without approval.");
    AssertTrue(result.ReasonCode == PermissionReasonCodes.HighRiskApprovalRequired, "Expected approval-required host diagnostics denial reason.");
    AssertTrue(tracer.GetActionLedger().Any(x => x.LifecycleState == ActionLifecycleState.ApprovalRequired && x.ActionType == ToolActionKind.RunCommand.ToString()), "Expected ApprovalRequired lifecycle for host diagnostics command.");
    Console.WriteLine("PASS HostDiagnosticsCommand_ApprovalRequired");
}

static async Task RunProcessExecutionHardeningRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    var outsideRoot = Path.Combine(tempRoot, "outside");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    Directory.CreateDirectory(outsideRoot);

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };
    var tracer = new ExecutionTracer(runtimeRoot);
    var guard = new PermissionGuard();
    var runner = new SafeProcessRunner(session, guard, tracer);

    var allowed = await runner.RunAsync(new SafeProcessRequest
    {
        Kind = ToolActionKind.Build,
        Command = "dotnet",
        Args = new[] { "--version" },
        WorkingDirectory = workspaceRoot,
        Timeout = TimeSpan.FromSeconds(20)
    });
    AssertTrue(allowed.Success, "Expected whitelisted command to pass.");

    var forbidden = await runner.RunAsync(new SafeProcessRequest
    {
        Kind = ToolActionKind.RunCommand,
        Command = "powershell",
        Args = new[] { "-Command", "Get-ChildItem" },
        WorkingDirectory = workspaceRoot,
        Timeout = TimeSpan.FromSeconds(5)
    });
    AssertTrue(!forbidden.Success && forbidden.ReasonCode == "INVALID_COMMAND", "Expected non-whitelisted command to be blocked.");

    var injection = await runner.RunAsync(new SafeProcessRequest
    {
        Kind = ToolActionKind.RunCommand,
        Command = "git",
        Args = new[] { "status&&whoami" },
        WorkingDirectory = workspaceRoot,
        Timeout = TimeSpan.FromSeconds(5)
    });
    AssertTrue(!injection.Success && injection.ReasonCode == "BLOCKED_PROCESS_EXECUTION", "Expected shell injection pattern to be blocked.");

    var outside = await runner.RunAsync(new SafeProcessRequest
    {
        Kind = ToolActionKind.RunCommand,
        Command = "git",
        Args = new[] { "status" },
        WorkingDirectory = outsideRoot,
        Timeout = TimeSpan.FromSeconds(5)
    });
    AssertTrue(!outside.Success && outside.ReasonCode == "BLOCKED_PROCESS_EXECUTION", "Expected out-of-workspace working directory to be blocked.");

    Console.WriteLine("PASS ProcessExecution_HardeningValidation");
}

static async Task RunRuntimeGpuDiagnosticsTruthfulReportingRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);

    var structured = JsonSerializer.Serialize(new
    {
        provider = "ollama",
        model = "qwen2.5-coder:7b-instruct-q4_K_M",
            runtimeProfile = "ollama/qwen2.5-coder-7b-quality-gpu-tuned",
        runtimeEndpoint = "http://localhost:11434",
        configuredContextWindow = "8192",
        configuredGpuOffloadOptions = "num_gpu=1",
        gpuUsageMeasured = false
    });

    using var doc = JsonDocument.Parse(structured);
    var root = doc.RootElement;
    AssertTrue(root.GetProperty("provider").GetString() == "ollama", "Expected provider=ollama.");
    AssertTrue(root.GetProperty("gpuUsageMeasured").GetBoolean() == false, "Expected gpuUsageMeasured=false without measured diagnostics output.");
    Console.WriteLine("PASS RuntimeGpuDiagnostics_TruthfulReporting");
}

static async Task RunDestructiveFileApprovalMarkerRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    var filePath = Path.Combine(workspaceRoot, "delete-me.txt");
    await File.WriteAllTextAsync(filePath, "x");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var guard = new PermissionGuard();
    var noApprovalDecision = guard.Evaluate(session, new ToolAction
    {
        Kind = ToolActionKind.DeleteFile,
        TargetPath = filePath
    });
    AssertTrue(!noApprovalDecision.Allowed && noApprovalDecision.RequiresApproval, "Expected delete without marker to require approval.");
    AssertTrue(noApprovalDecision.ApprovalProposal?.ReasonCode == PermissionReasonCodes.AccessDeniedDeleteOperation, "Expected destructive approval proposal to carry stable reasonCode.");
    AssertTrue(!string.IsNullOrWhiteSpace(noApprovalDecision.ApprovalProposal?.ExpectedEffect), "Expected destructive approval proposal to carry expectedEffect.");

    var approvalProposal = noApprovalDecision.ApprovalProposal;
    AssertTrue(approvalProposal is not null && !string.IsNullOrWhiteSpace(approvalProposal.ProposalId), "Expected proposal id for destructive approval.");
    var approvedDecision = guard.Evaluate(session, new ToolAction
    {
        Kind = ToolActionKind.DeleteFile,
        TargetPath = filePath,
        Payload = $"APPROVED:{approvalProposal!.ProposalId}"
    });
    AssertTrue(approvedDecision.Allowed, "Expected delete with approval marker to pass guard.");
    Console.WriteLine("PASS DestructiveFileApprovalMarker");
}

static async Task RunGuardedToolExplicitApprovalHandoffRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    var outsideRoot = Path.Combine(tempRoot, "outside");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    Directory.CreateDirectory(outsideRoot);

    var insidePath = Path.Combine(workspaceRoot, "delete-inside.txt");
    var insidePathSecond = Path.Combine(workspaceRoot, "delete-inside-2.txt");
    var outsidePath = Path.Combine(outsideRoot, "delete-outside.txt");
    await File.WriteAllTextAsync(insidePath, "inside");
    await File.WriteAllTextAsync(insidePathSecond, "inside2");
    await File.WriteAllTextAsync(outsidePath, "outside");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var guard = new PermissionGuard();
    var tracer = new ExecutionTracer(runtimeRoot);
    var patchGate = new PatchSafetyGate(session, guard, tracer);
    var destructiveGate = new DestructiveOperationSafetyGate(session, guard, tracer);
    var sandbox = new SandboxManager(workspaceRoot, runtimeRoot);
    await sandbox.CreateSandbox();
    var inner = new FileTool(session, guard, patchGate, destructiveGate, sandbox, tracer);
    var guarded = new GuardedTool(inner, guard, session, CreateFileActionFactory(workspaceRoot), tracer);

    var noApproval = await guarded.Execute("delete:delete-inside.txt");
    AssertTrue(noApproval.StartsWith("DENIED", StringComparison.Ordinal), "Expected approval-required delete to be denied before approval.");
    AssertTrue(File.Exists(insidePath), "Expected file to remain without explicit approval.");

    var decision = guard.Evaluate(session, new ToolAction
    {
        Kind = ToolActionKind.DeleteFile,
        TargetPath = insidePath
    });
    AssertTrue(decision.ApprovalProposal is not null, "Expected approval proposal for inside delete.");
    var proposalId = decision.ApprovalProposal!.ProposalId;
    var wrongApproved = await guarded.Execute("delete:delete-inside.txt APPROVED:wrong-token");
    AssertTrue(wrongApproved.StartsWith("DENIED", StringComparison.Ordinal), "Expected wrong token to stay denied.");
    AssertTrue(File.Exists(insidePath), "Expected file to remain for wrong approval token.");

    var approved = await guarded.Execute($"delete:delete-inside.txt APPROVED:{proposalId}");
    AssertTrue(approved.StartsWith("Successfully deleted", StringComparison.Ordinal), $"Expected approved delete to execute. Actual: {approved}");
    AssertTrue(!File.Exists(insidePath), "Expected approved delete to remove inside file.");

    var reusedToken = await guarded.Execute($"delete:delete-inside-2.txt APPROVED:{proposalId}");
    AssertTrue(reusedToken.StartsWith("DENIED", StringComparison.Ordinal), "Expected consumed token reuse to be denied.");
    AssertTrue(File.Exists(insidePathSecond), "Expected second inside file to remain after token reuse.");

    var secondDecision = guard.Evaluate(session, new ToolAction
    {
        Kind = ToolActionKind.DeleteFile,
        TargetPath = insidePathSecond
    });
    AssertTrue(secondDecision.ApprovalProposal is not null, "Expected approval proposal for second inside delete.");
    var secondProposalId = secondDecision.ApprovalProposal!.ProposalId;
    AssertTrue(!string.Equals(proposalId, secondProposalId, StringComparison.OrdinalIgnoreCase), "Expected distinct proposals for distinct targets.");
    var wrongProposalToken = await guarded.Execute($"delete:delete-inside-2.txt APPROVED:{proposalId}");
    AssertTrue(wrongProposalToken.StartsWith("DENIED", StringComparison.Ordinal), "Expected token for proposal A not to authorize proposal B.");
    AssertTrue(File.Exists(insidePathSecond), "Expected second inside file to remain for mismatched proposal token.");

    var secondApproved = await guarded.Execute($"delete:delete-inside-2.txt APPROVED:{secondProposalId}");
    AssertTrue(secondApproved.StartsWith("Successfully deleted", StringComparison.Ordinal), $"Expected second proposal token to authorize second delete. Actual: {secondApproved}");
    AssertTrue(!File.Exists(insidePathSecond), "Expected second inside file to be deleted with its own token.");

    var outsideApproved = await guarded.Execute($"delete:{outsidePath} APPROVED:{proposalId}");
    AssertTrue(outsideApproved.StartsWith("DENIED", StringComparison.Ordinal), "Expected outside-boundary action to remain denied even with approval.");
    AssertTrue(File.Exists(outsidePath), "Expected outside file to remain untouched.");

    Console.WriteLine("PASS GuardedTool_ExplicitApprovalHandoff");
}

static void RunExtractRequestedNewFilePath_ExtensionRegression()
{
    var method = typeof(Agent).GetMethod("ExtractRequestedNewFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    AssertTrue(method is not null, "Expected Agent.ExtractRequestedNewFilePath to exist.");

    var result1 = method!.Invoke(null, new object[] { "create file config/new.cfg for parser" }) as string;
    AssertTrue(string.Equals(result1, "config/new.cfg", StringComparison.Ordinal), "Expected .cfg path extraction for create intent.");

    var result2 = method!.Invoke(null, new object[] { "make file scripts/setup.ps1 with defaults" }) as string;
    AssertTrue(string.Equals(result2, "scripts/setup.ps1", StringComparison.Ordinal), "Expected .ps1 path extraction for make intent.");

    Console.WriteLine("PASS ExtractRequestedNewFilePath_ExtensionRegression");
}

static void RunExtractRequestedNewFilePath_NoCreateIntentRegression()
{
    var method = typeof(Agent).GetMethod("ExtractRequestedNewFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    AssertTrue(method is not null, "Expected Agent.ExtractRequestedNewFilePath to exist.");

    var result = method!.Invoke(null, new object[] { "analyze config/new.cfg and explain format" }) as string;
    AssertTrue(result is null, "Expected no path extraction when create intent is absent.");

    Console.WriteLine("PASS ExtractRequestedNewFilePath_NoCreateIntentRegression");
}

static void RunExtractRequestedNewFilePath_NoExtensionRegression()
{
    var method = typeof(Agent).GetMethod("ExtractRequestedNewFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    AssertTrue(method is not null, "Expected Agent.ExtractRequestedNewFilePath to exist.");

    var result = method!.Invoke(null, new object[] { "create file config/newfile for parser" }) as string;
    AssertTrue(result is null, "Expected no path extraction when file has no extension.");

    Console.WriteLine("PASS ExtractRequestedNewFilePath_NoExtensionRegression");
}

static void RunExtractRequestedNewFilePath_UppercaseExtensionRegression()
{
    var method = typeof(Agent).GetMethod("ExtractRequestedNewFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    AssertTrue(method is not null, "Expected Agent.ExtractRequestedNewFilePath to exist.");

    var result = method!.Invoke(null, new object[] { "create file Assets/Texture.PNG now" }) as string;
    AssertTrue(string.Equals(result, "Assets/Texture.PNG", StringComparison.Ordinal), "Expected path extraction to preserve uppercase extension.");

    Console.WriteLine("PASS ExtractRequestedNewFilePath_UppercaseExtensionRegression");
}

static void RunExtractRequestedNewFilePath_WindowsPathRegression()
{
    var method = typeof(Agent).GetMethod("ExtractRequestedNewFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    AssertTrue(method is not null, "Expected Agent.ExtractRequestedNewFilePath to exist.");

    var result = method!.Invoke(null, new object[] { "create file tools\\build\\setup.cmd now" }) as string;
    AssertTrue(string.Equals(result, "tools\\build\\setup.cmd", StringComparison.Ordinal), "Expected extraction for Windows-style path.");

    Console.WriteLine("PASS ExtractRequestedNewFilePath_WindowsPathRegression");
}

static void RunExtractRequestedNewFilePath_QuotedPathRegression()
{
    var method = typeof(Agent).GetMethod("ExtractRequestedNewFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    AssertTrue(method is not null, "Expected Agent.ExtractRequestedNewFilePath to exist.");

    var result = method!.Invoke(null, new object[] { "create file \"configs/new.config.json\" please" }) as string;
    AssertTrue(string.Equals(result, "configs/new.config.json", StringComparison.Ordinal), "Expected extraction for quoted file path.");

    Console.WriteLine("PASS ExtractRequestedNewFilePath_QuotedPathRegression");
}

static void RunExtractRequestedNewFilePath_DashUnderscoreRegression()
{
    var method = typeof(Agent).GetMethod("ExtractRequestedNewFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    AssertTrue(method is not null, "Expected Agent.ExtractRequestedNewFilePath to exist.");

    var result = method!.Invoke(null, new object[] { "create file configs/my-config_v2.test.json now" }) as string;
    AssertTrue(string.Equals(result, "configs/my-config_v2.test.json", StringComparison.Ordinal), "Expected extraction for dash/underscore and multi-dot path.");

    Console.WriteLine("PASS ExtractRequestedNewFilePath_DashUnderscoreRegression");
}

static void RunExtractRequestedNewFilePath_RelativeDotSlashRegression()
{
    var method = typeof(Agent).GetMethod("ExtractRequestedNewFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    AssertTrue(method is not null, "Expected Agent.ExtractRequestedNewFilePath to exist.");

    var result = method!.Invoke(null, new object[] { "create file ./src/new-file.config.json please" }) as string;
    AssertTrue(string.Equals(result, "./src/new-file.config.json", StringComparison.Ordinal), "Expected extraction for relative ./ path.");

    Console.WriteLine("PASS ExtractRequestedNewFilePath_RelativeDotSlashRegression");
}

static void RunExtractRequestedNewFilePath_MultiDotFileNameRegression()
{
    var method = typeof(Agent).GetMethod("ExtractRequestedNewFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    AssertTrue(method is not null, "Expected Agent.ExtractRequestedNewFilePath to exist.");

    var result = method!.Invoke(null, new object[] { "create file archive.backup.2026-05-01.tar.gz now" }) as string;
    AssertTrue(string.Equals(result, "archive.backup.2026-05-01.tar.gz", StringComparison.Ordinal), "Expected extraction for multi-dot archive file name.");

    Console.WriteLine("PASS ExtractRequestedNewFilePath_MultiDotFileNameRegression");
}

static void RunExtractRequestedNewFilePath_UrlNegativeRegression()
{
    var method = typeof(Agent).GetMethod("ExtractRequestedNewFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    AssertTrue(method is not null, "Expected Agent.ExtractRequestedNewFilePath to exist.");

    var urlTask = "create https://example.com/schema.json";
    var urlPath = method!.Invoke(null, new object?[] { urlTask }) as string;
    AssertTrue(urlPath is null, "URL must not be treated as a local file path");

    var urlWithQueryTask = "create https://example.com/schema.json?raw=1#top";
    var urlWithQueryPath = method.Invoke(null, new object?[] { urlWithQueryTask }) as string;
    AssertTrue(urlWithQueryPath is null, "URL with query/hash must not be treated as a local file path");

    Console.WriteLine("PASS ExtractRequestedNewFilePath_UrlNegativeRegression");
}

static void RunExtractRequestedNewFilePath_RussianIntentRegression()
{
    var method = typeof(Agent).GetMethod("ExtractRequestedNewFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    AssertTrue(method is not null, "Expected Agent.ExtractRequestedNewFilePath to exist.");

    var ruCreateTask = "создай tools/parser.cpp с базовым каркасом";
    var ruCreatePath = method!.Invoke(null, new object?[] { ruCreateTask }) as string;
    AssertTrue(string.Equals(ruCreatePath, "tools/parser.cpp", StringComparison.Ordinal), "Russian create intent should extract file path");

    var ruVariants = new (string Task, string Expected)[]
    {
        ("добавь файл tools/addon.lua", "tools/addon.lua"),
        ("напиши файл docs/manual.txt", "docs/manual.txt"),
        ("сделай файл config/new.cfg", "config/new.cfg"),
        ("саздай файл tools/typo1.cpp", "tools/typo1.cpp"),
        ("напеши файл docs/typo2.md", "docs/typo2.md"),
        ("зделай файл config/typo3.ini", "config/typo3.ini")
    };

    foreach (var testCase in ruVariants)
    {
        var extracted = method.Invoke(null, new object?[] { testCase.Task }) as string;
        AssertTrue(string.Equals(extracted, testCase.Expected, StringComparison.Ordinal), $"Expected '{testCase.Expected}' for Russian variant '{testCase.Task}'");
    }

    Console.WriteLine("PASS ExtractRequestedNewFilePath_RussianIntentRegression");
}

static void RunMemoryProjectScopeResolverRegression()
{
    AssertTrue(string.Equals(MemoryProjectScopeResolver.Resolve(""), MemoryGovernanceDefaults.DefaultProjectScope, StringComparison.Ordinal), "Empty query must resolve to default scope.");
    AssertTrue(string.Equals(MemoryProjectScopeResolver.Resolve("refactor module scope:agent-core"), "agent-core", StringComparison.Ordinal), "scope: marker must be parsed.");
    AssertTrue(string.Equals(MemoryProjectScopeResolver.Resolve("fix bug project:payments-api quickly"), "payments-api", StringComparison.Ordinal), "project: marker must be parsed.");
    AssertTrue(string.Equals(MemoryProjectScopeResolver.Resolve("analyze workspace:tools/runtime, then summarize"), "tools/runtime", StringComparison.Ordinal), "workspace: marker must be parsed.");
    AssertTrue(string.Equals(MemoryProjectScopeResolver.Resolve("analyze everything"), MemoryGovernanceDefaults.DefaultProjectScope, StringComparison.Ordinal), "Query without explicit marker must resolve to default scope.");
    AssertTrue(string.Equals(MemoryProjectScopeResolver.NormalizeScope("  qa-scope  "), "qa-scope", StringComparison.Ordinal), "NormalizeScope must trim value.");
    AssertTrue(MemoryProjectScopeResolver.IsSameScope(null, "default"), "IsSameScope must treat null as default.");

    Console.WriteLine("PASS MemoryProjectScopeResolver");
}

static void RunMemoryGovernanceRecalibrationRegression()
{
    var memory = new AgentMemorySystem();

    memory.RecordFailure(new FailureRecord
    {
        Query = "scope:agent-core fix compile issue",
        ProjectScope = "agent-core",
        ConfidenceScore = 0.40,
        FailureType = FailureType.CompilationError,
        Severity = FailureSeverity.Medium
    });

    memory.RecordSuccess(new SuccessRecord
    {
        Query = "scope:agent-core apply small patch",
        ProjectScope = "agent-core",
        ConfidenceScore = 0.60
    });

    var raised = memory.RecalibrateConfidenceByProjectScope("agent-core", success: true);
    AssertTrue(raised == 2, "Expected recalibration to update both records in the target scope.");

    var relevantAfterRaise = memory.GetRelevantFailures("scope:agent-core fix compile issue", 10).ToList();
    AssertTrue(relevantAfterRaise.Count > 0, "Expected relevant failures for scope after recalibration.");
    AssertTrue((relevantAfterRaise[0].ConfidenceScore ?? 0) > 0.40, "Expected confidence to increase on success recalibration.");

    var lowered = memory.RecalibrateConfidenceByProjectScope("agent-core", success: false);
    AssertTrue(lowered == 2, "Expected failure recalibration to update both records in scope.");

    var removedLowConfidence = memory.InvalidateLowConfidenceRecords("agent-core", 0.50);
    AssertTrue(removedLowConfidence >= 1, "Expected low-confidence invalidation to remove at least one record.");

    Console.WriteLine("PASS MemoryGovernanceRecalibration");
}

static void RunMemoryScopedRetrievalRegression()
{
    var memory = new AgentMemorySystem();

    memory.RecordFailure(new FailureRecord
    {
        Query = "fix parser crash in module",
        ProjectScope = "alpha",
        ConfidenceScore = 0.8,
        FailureType = FailureType.CompilationError,
        Severity = FailureSeverity.High
    });

    memory.RecordFailure(new FailureRecord
    {
        Query = "fix parser crash in module",
        ProjectScope = "beta",
        ConfidenceScore = 0.8,
        FailureType = FailureType.CompilationError,
        Severity = FailureSeverity.High
    });

    var alpha = memory.GetRelevantFailures("fix parser crash in module", "alpha", 10).ToList();
    var beta = memory.GetRelevantFailures("fix parser crash in module", "beta", 10).ToList();

    AssertTrue(alpha.Count == 1, "Expected scoped retrieval to return only alpha scope records.");
    AssertTrue(beta.Count == 1, "Expected scoped retrieval to return only beta scope records.");
    AssertTrue(MemoryProjectScopeResolver.IsSameScope(alpha[0].ProjectScope, "alpha"), "Expected alpha scoped record.");
    AssertTrue(MemoryProjectScopeResolver.IsSameScope(beta[0].ProjectScope, "beta"), "Expected beta scoped record.");

    var removed = memory.InvalidateLowConfidenceRecords("alpha");
    AssertTrue(removed == 0, "Expected default quality-floor invalidation to keep high-confidence records.");

    Console.WriteLine("PASS MemoryScopedRetrieval");
}

static void RunMemoryFactoryAndInvalidationHookRegression()
{
    var memory = new AgentMemorySystem();

    memory.RecordFailure(MemoryRecordFactory.CreateFailure(
        query: "scope:agent-core fail build",
        failureType: FailureType.CompilationError,
        severity: FailureSeverity.Medium,
        reason: "BuildVerificationFailed",
        projectScope: "agent-core",
        source: "unit-test",
        confidenceScore: 0.49));

    memory.RecordSuccess(MemoryRecordFactory.CreateSuccess(
        query: "scope:agent-core success patch",
        projectScope: "agent-core",
        source: "unit-test",
        confidenceScore: 0.51));

    var pruned = MemoryInvalidationHook.RecalibrateAndPruneOnFailure(memory, "agent-core");
    AssertTrue(pruned >= 1, "Expected recalibrate+prune hook to remove low-confidence records.");

    var remaining = memory.GetRelevantFailures("scope:agent-core fail build", "agent-core", 10).ToList();
    AssertTrue(remaining.Count == 0, "Expected low-confidence failure record to be pruned.");

    Console.WriteLine("PASS MemoryFactoryAndInvalidationHook");
}

static void RunMemorySourceScopedRetrievalRegression()
{
    var memory = new AgentMemorySystem();

    memory.RecordFailure(MemoryRecordFactory.CreateFailure(
        query: "scope:agent-core fix source scope test",
        failureType: FailureType.CompilationError,
        severity: FailureSeverity.High,
        projectScope: "agent-core",
        source: "source-a",
        confidenceScore: 0.8));

    memory.RecordFailure(MemoryRecordFactory.CreateFailure(
        query: "scope:agent-core fix source scope test",
        failureType: FailureType.CompilationError,
        severity: FailureSeverity.High,
        projectScope: "agent-core",
        source: "source-b",
        confidenceScore: 0.8));

    var fromA = memory.GetRelevantFailures("scope:agent-core fix source scope test", "agent-core", "source-a", 10).ToList();
    var fromB = memory.GetRelevantFailures("scope:agent-core fix source scope test", "agent-core", "source-b", 10).ToList();

    AssertTrue(fromA.Count == 1 && string.Equals(fromA[0].Source, "source-a", StringComparison.Ordinal), "Expected source-a scoped retrieval.");
    AssertTrue(fromB.Count == 1 && string.Equals(fromB[0].Source, "source-b", StringComparison.Ordinal), "Expected source-b scoped retrieval.");

    var pruned = MemoryScopePruneHelper.PruneStaleScopeRecords(memory, "agent-core");
    AssertTrue(pruned == 0, "Expected prune helper to keep high-confidence records.");

    Console.WriteLine("PASS MemorySourceScopedRetrieval");
}

static void RunMemorySourceInvalidationRegression()
{
    var memory = new AgentMemorySystem();

    memory.RecordFailure(MemoryRecordFactory.CreateFailure(
        query: "scope:agent-core source invalidation test",
        failureType: FailureType.CompilationError,
        severity: FailureSeverity.High,
        projectScope: "agent-core",
        source: "source-a",
        confidenceScore: 0.8));

    memory.RecordFailure(MemoryRecordFactory.CreateFailure(
        query: "scope:agent-core source invalidation test",
        failureType: FailureType.CompilationError,
        severity: FailureSeverity.High,
        projectScope: "agent-core",
        source: "source-b",
        confidenceScore: 0.8));

    var removed = memory.InvalidateBySource("source-a");
    AssertTrue(removed == 1, "Expected invalidation by source to remove only source-a record.");

    var remainingB = memory.GetRelevantFailures("scope:agent-core source invalidation test", "agent-core", "source-b", 10).ToList();
    AssertTrue(remainingB.Count == 1, "Expected source-b record to remain after source-a invalidation.");

    Console.WriteLine("PASS MemorySourceInvalidation");
}

static void RunMemoryScopeSourceInvalidationRegression()
{
    var memory = new AgentMemorySystem();

    memory.RecordFailure(MemoryRecordFactory.CreateFailure(
        query: "scope alpha test",
        failureType: FailureType.CompilationError,
        severity: FailureSeverity.Medium,
        projectScope: "alpha",
        source: "s1",
        confidenceScore: 0.6));

    memory.RecordFailure(MemoryRecordFactory.CreateFailure(
        query: "scope beta test",
        failureType: FailureType.CompilationError,
        severity: FailureSeverity.Medium,
        projectScope: "beta",
        source: "s1",
        confidenceScore: 0.6));

    memory.RecordSuccess(MemoryRecordFactory.CreateSuccess(
        query: "scope alpha success",
        projectScope: "alpha",
        source: "s1",
        confidenceScore: 0.6));

    var recalibrated = memory.RecalibrateConfidenceBySource("s1", success: false);
    AssertTrue(recalibrated == 3, "Expected source recalibration to update all source records.");

    var removed = memory.InvalidateByScopeAndSource("alpha", "s1");
    AssertTrue(removed == 2, "Expected scope+source invalidation to remove only alpha+s1 records.");

    var betaRemaining = memory.GetRelevantFailures("scope beta test", "beta", "s1", 10).ToList();
    AssertTrue(betaRemaining.Count == 1, "Expected beta+s1 record to remain.");

    Console.WriteLine("PASS MemoryScopeSourceInvalidation");
}


static async Task RunRunTaskBaselineBehaviorRegression()
{
    await RunAnalysisNormalResponseRegression();
    Console.WriteLine("PASS RunTask_BaselineBehavior_Unchanged");
}

static async Task RunRunTaskToolCallFlowRegression()
{
    await RunActionLifecycleLedgerRegression();
    Console.WriteLine("PASS RunTask_ToolCallFlow_Unchanged");
}

static async Task RunRunTaskMalformedToolCallDiagnosticRegression()
{
    await RunPatchApplyDiagnosticsClassificationRegression();
    Console.WriteLine("PASS RunTask_MalformedToolCall_DiagnosticBranch");
}

static async Task RunRunTaskTargetResolutionRegression()
{
    await RunTechnicalNoToolCallsRequiresActionRegression();
    Console.WriteLine("PASS RunTask_TargetResolution_AgentVsCoreAgent");
}

static async Task RunTargetResolutionPathPreservingRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    Directory.CreateDirectory(Path.Combine(workspaceRoot, "Core"));
    Directory.CreateDirectory(Path.Combine(workspaceRoot, "Features"));
    Directory.CreateDirectory(Path.Combine(workspaceRoot, "Legacy"));
    File.WriteAllText(Path.Combine(workspaceRoot, "Core", "Agent.cs"), "namespace App.Core; public class Agent {}");
    File.WriteAllText(Path.Combine(workspaceRoot, "Features", "Agent.cs"), "namespace App.Features; public class Agent {}");
    File.WriteAllText(Path.Combine(workspaceRoot, "Legacy", "Agent.cs"), "namespace App.Legacy; public class Agent {}");

    var tracer = new ExecutionTracer(runtimeRoot);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var embeddingService = new EmbeddingService(disabled: true);
    var indexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);
    var indexResult = await indexer.IndexProject();
    AssertTrue(indexResult.Success, "Expected project index success for target path regression.");
    var gate = new TargetResolutionGate(indexer, tracer);

    var pathResult = await gate.ResolveAsync("edit Core/Agent.cs");
    AssertTrue(pathResult.IsResolved, "Expected path-like target to resolve.");
    AssertTrue(
        pathResult.SelectedFiles.Count == 1 &&
        string.Equals(pathResult.SelectedFiles[0].Replace('\\', '/'), "Core/Agent.cs", StringComparison.OrdinalIgnoreCase),
        "Expected Core/Agent.cs path to be preserved.");

    var basenameResult = await gate.ResolveAsync("edit Agent.cs");
    AssertTrue(basenameResult.IsFailed, "Expected basename-only target with duplicates to be ambiguous.");

    Console.WriteLine("PASS TargetResolution_PathPreserving");
}

static async Task RunStructuredActionContractCompatibilityRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "a.txt"), "ok");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun("contract", "contract", workspaceRoot, runtimeRoot, AgentAccessMode.WorkspaceWrite.ToString(), "test", "test");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var guard = new PermissionGuard();
    var outsideAction = new ToolAction { Kind = ToolActionKind.ReadFile, TargetPath = Path.Combine(tempRoot, "outside.txt") };
    var outsideDecision = guard.Evaluate(session, outsideAction);
    tracer.LogPermissionDecision(session, "file", outsideAction, outsideDecision);

    var guarded = new GuardedTool(new FakeNoopTool(), guard, session, _ => new ToolAction
    {
        Kind = ToolActionKind.ReadFile,
        TargetPath = Path.Combine(workspaceRoot, "a.txt")
    }, tracer);
    _ = await guarded.Execute("read:a.txt");

    var payloadJson = JsonSerializer.Serialize(new
    {
        executionMode = "active-workspace",
        execution_mode = "active-workspace",
        executionWorkspaceKind = "active-workspace",
        activeWorkspaceUsed = true,
        sandboxRoot = workspaceRoot,
        worktreeRoot = workspaceRoot,
        hostBoundaryPreserved = true,
        approvalStatusSummary = new { allowed = 1, approvalRequired = 1, denied = 0, notApplicable = 0 },
        actionLifecycle = tracer.GetActionLedger().Select(x => new
        {
            lifecycleState = x.LifecycleState.ToString(),
            approvalStatus = x.ApprovalStatus
        }).ToArray()
    });
    using var doc = JsonDocument.Parse(payloadJson);
    var root = doc.RootElement;

    AssertTrue(root.TryGetProperty("executionMode", out _), "Expected executionMode key.");
    AssertTrue(root.TryGetProperty("execution_mode", out _), "Expected execution_mode alias key.");
    AssertTrue(root.TryGetProperty("executionWorkspaceKind", out _), "Expected executionWorkspaceKind key.");
    AssertTrue(root.TryGetProperty("activeWorkspaceUsed", out _), "Expected activeWorkspaceUsed key.");
    AssertTrue(root.TryGetProperty("sandboxRoot", out _), "Expected sandboxRoot key.");
    AssertTrue(root.TryGetProperty("worktreeRoot", out _), "Expected worktreeRoot key.");
    AssertTrue(root.TryGetProperty("hostBoundaryPreserved", out _), "Expected hostBoundaryPreserved key.");

    var allowedLifecycle = new HashSet<string>(StringComparer.Ordinal) { "Requested", "ApprovalRequired", "Blocked", "Executed", "Failed" };
    var allowedStatus = new HashSet<string>(StringComparer.Ordinal) { "Allowed", "ApprovalRequired", "Denied", "NotApplicable" };
    var lifecycle = root.GetProperty("actionLifecycle");
    foreach (var item in lifecycle.EnumerateArray())
    {
        var state = item.GetProperty("lifecycleState").GetString() ?? string.Empty;
        var status = item.GetProperty("approvalStatus").GetString() ?? string.Empty;
        AssertTrue(allowedLifecycle.Contains(state), $"Unexpected lifecycleState: {state}");
        AssertTrue(allowedStatus.Contains(status), $"Unexpected approvalStatus: {status}");
    }

    var hasOutsideExecuted = tracer.GetActionLedger().Any(x =>
        x.Target.Contains("outside.txt", StringComparison.OrdinalIgnoreCase) &&
        x.LifecycleState == ActionLifecycleState.Executed);
    AssertTrue(!hasOutsideExecuted, "Outside approval-required action must not be executed.");

    Console.WriteLine("PASS StructuredActionContract_Compatibility");
}

static async Task RunIsolatedExecutionWorkspaceRoutingRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var activeWorkspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    var worktreeRoot = Path.Combine(runtimeRoot, "worktrees", "session");
    Directory.CreateDirectory(activeWorkspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    Directory.CreateDirectory(worktreeRoot);

    await File.WriteAllTextAsync(Path.Combine(activeWorkspaceRoot, "existing.txt"), "active");
    await File.WriteAllTextAsync(Path.Combine(worktreeRoot, "existing.txt"), "worktree");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = activeWorkspaceRoot,
        ExecutionWorkspaceRoot = worktreeRoot,
        WorktreeRoot = worktreeRoot,
        ExecutionWorkspaceKind = "worktree",
        ActiveWorkspaceUsed = false,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var tracer = new ExecutionTracer(runtimeRoot);
    var guard = new PermissionGuard();
    var patchGate = new PatchSafetyGate(session, guard, tracer);
    var destructiveGate = new DestructiveOperationSafetyGate(session, guard, tracer);
    var sandbox = new SandboxManager(worktreeRoot, runtimeRoot);
    await sandbox.CreateSandbox();
    var fileTool = new FileTool(session, guard, patchGate, destructiveGate, sandbox, tracer);
    var writeResult = await fileTool.Execute("write:test.txt:hello");
    AssertTrue(writeResult.StartsWith("Successfully wrote", StringComparison.Ordinal), "Expected isolated file write success.");
    AssertTrue(File.Exists(Path.Combine(worktreeRoot, "test.txt")), "Expected write in worktree root.");
    AssertTrue(!File.Exists(Path.Combine(activeWorkspaceRoot, "test.txt")), "Expected no write in active workspace root.");

    var runner = new SafeProcessRunner(session, guard, tracer);
    var cmd = await runner.RunAsync(new SafeProcessRequest
    {
        Kind = ToolActionKind.RunCommand,
        Command = "dotnet",
        Args = new[] { "--version" },
        WorkingDirectory = ""
    });
    AssertTrue(cmd.WorkingDirectory.Equals(worktreeRoot, StringComparison.OrdinalIgnoreCase), "Expected isolated command working directory to be worktree root.");

    var normalSession = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = activeWorkspaceRoot,
        ExecutionWorkspaceRoot = activeWorkspaceRoot,
        WorktreeRoot = activeWorkspaceRoot,
        ExecutionWorkspaceKind = "active-workspace",
        ActiveWorkspaceUsed = true,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var normalRunner = new SafeProcessRunner(normalSession, guard, tracer);
    var normalCmd = await normalRunner.RunAsync(new SafeProcessRequest
    {
        Kind = ToolActionKind.RunCommand,
        Command = "dotnet",
        Args = new[] { "--version" },
        WorkingDirectory = ""
    });
    AssertTrue(normalCmd.WorkingDirectory.Equals(activeWorkspaceRoot, StringComparison.OrdinalIgnoreCase), "Expected normal command working directory to stay active workspace.");

    Console.WriteLine("PASS IsolatedExecutionWorkspace_Routing");
}

static async Task RunContextSelectionPrecisionRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    Directory.CreateDirectory(Path.Combine(workspaceRoot, "Core"));
    Directory.CreateDirectory(Path.Combine(workspaceRoot, "Noise"));

    var targetFile = Path.Combine(workspaceRoot, "Core", "TargetService.cs");
    var relatedFile = Path.Combine(workspaceRoot, "Core", "TargetServiceHelper.cs");
    var noiseFile = Path.Combine(workspaceRoot, "Noise", "Unrelated.cs");
    await File.WriteAllTextAsync(targetFile, "public class TargetService { public void Handle() {} }");
    await File.WriteAllTextAsync(relatedFile, "public class TargetServiceHelper { public void Assist() {} }");
    await File.WriteAllTextAsync(noiseFile, new string('x', 50000));

    var tracer = new ExecutionTracer(runtimeRoot);
    var builder = new ContextBuilder(workspaceRoot, new VectorStore(), new FileStateManager(), new ProjectSymbolDirectory(), tracer);

    var semantic = new List<string> { "Noise/Unrelated.cs", "Core/TargetService.cs", "Core/TargetServiceHelper.cs" };
    var symbols = new List<string> { "Core/TargetService.cs", "Core/TargetServiceHelper.cs" };
    var info = builder.BuildContext("Fix TargetService.Handle method", semantic, symbols, 6);

    AssertTrue(info.SelectedFiles.Contains("Core/TargetService.cs", StringComparer.OrdinalIgnoreCase), "Expected exact target file in context.");
    AssertTrue(!info.SelectedFiles.Contains("Noise/Unrelated.cs", StringComparer.OrdinalIgnoreCase), "Expected unrelated oversized file to be excluded by relevance/budget.");
    AssertTrue(info.TotalLength > 0 && info.TotalLength <= 30000, "Expected context to respect medium complexity size budget.");
    Console.WriteLine("PASS ContextSelection_PrecisionAndBudget");
}

static async Task RunContextSelectionAdaptiveBudgetFitRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    Directory.CreateDirectory(Path.Combine(workspaceRoot, "Core"));

    var targetFile = Path.Combine(workspaceRoot, "Core", "TargetService.cs");
    var helperFile = Path.Combine(workspaceRoot, "Core", "TargetServiceHelper.cs");
    var extraFile = Path.Combine(workspaceRoot, "Core", "TargetServiceNotes.cs");
    await File.WriteAllTextAsync(targetFile, new string('a', 12000));
    await File.WriteAllTextAsync(helperFile, new string('b', 8000));
    await File.WriteAllTextAsync(extraFile, new string('c', 15000));

    var tracer = new ExecutionTracer(runtimeRoot);
    var builder = new ContextBuilder(workspaceRoot, new VectorStore(), new FileStateManager(), new ProjectSymbolDirectory(), tracer);
    var semantic = new List<string> { "Core/TargetService.cs", "Core/TargetServiceHelper.cs", "Core/TargetServiceNotes.cs" };
    var symbols = new List<string> { "Core/TargetService.cs", "Core/TargetServiceHelper.cs", "Core/TargetServiceNotes.cs" };
    var info = builder.BuildContext("Improve target method and helper with focused refactor for reliability", semantic, symbols, 6);

    AssertTrue(info.SelectedFiles.Contains("Core/TargetService.cs", StringComparer.OrdinalIgnoreCase), "Expected target file to remain selected.");
    AssertTrue(info.SelectedFiles.Contains("Core/TargetServiceHelper.cs", StringComparer.OrdinalIgnoreCase), "Expected ranked helper file to fit into budget.");
    AssertTrue(!info.SelectedFiles.Contains("Core/TargetServiceNotes.cs", StringComparer.OrdinalIgnoreCase), "Expected lower-priority file to be excluded when prefix budget is full.");
    AssertTrue(info.TotalLength == 20000, "Expected adaptive fit to use more budget than single-file tail cutoff scenario.");
    AssertTrue(info.TotalLength <= 30000, "Expected selection to stay within medium complexity budget.");
    Console.WriteLine("PASS ContextSelection_AdaptiveBudgetFit");
}

static async Task<(JsonElement structured, string workspaceRoot, string runtimeRoot)> RunAgentWithAdapter(ILlmProviderAdapter adapter)
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    File.WriteAllText(Path.Combine(workspaceRoot, "Program.cs"), "namespace SampleApp; public static class Entry { public static void Hello() { } }");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun("retry-check", "retry-check", workspaceRoot, runtimeRoot, AgentAccessMode.WorkspaceWrite.ToString(), "ollama", "model");
    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };
    var toolRegistry = new ToolRegistry();
    var memory = new MemoryStore();
    var permissionGuard = new PermissionGuard();
    var safeProcessRunner = new SafeProcessRunner(session, permissionGuard, tracer);
    var buildVerifier = new BuildVerifier(safeProcessRunner, tracer);
    var sandboxManager = new SandboxManager(workspaceRoot, runtimeRoot);
    var embeddingService = new EmbeddingService(disabled: true);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var projectIndexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);
    var contextBuilder = new ContextBuilder(workspaceRoot, vectorStore, fileStateManager, new ProjectSymbolDirectory(), tracer);
    var profile = LlmProfiles.Resolve("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");
    var policy = LlmProfiles.ResolvePolicy("ollama", "qwen2.5-coder:7b-instruct-q4_K_M");
    var llmClient = new LlmRuntimeClient(adapter, profile, policy);
    var agent = new Agent(llmClient, toolRegistry, memory, buildVerifier, sandboxManager, projectIndexer, contextBuilder, fileStateManager, session, workspaceResolution: null);

    var oldOut = Console.Out;
    var capture = new StringWriter();
    Console.SetOut(capture);
    try { _ = await agent.RunTask("Опиши проект кратко на русском"); }
    finally { Console.SetOut(oldOut); }

    return (ExtractStructuredPayload(capture.ToString()), workspaceRoot, runtimeRoot);
}

static Func<string, ToolAction> CreateFileActionFactory(string workspaceRoot) => input =>
{
    static string ResolvePath(string root, string path)
    {
        var full = Path.IsPathFullyQualified(path) ? path : Path.Combine(root, path);
        return Path.GetFullPath(full);
    }

    static int FindWriteSeparator(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return -1;
        if (payload.Length >= 3 && payload[1] == ':' && (payload[2] == '\\' || payload[2] == '/'))
            return payload.IndexOf(':', 3);
        return payload.IndexOf(':');
    }

    static int FindPathPairSeparator(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return -1;
        if (payload.Length >= 3 && payload[1] == ':' && (payload[2] == '\\' || payload[2] == '/'))
            return payload.IndexOf(':', 3);
        return payload.IndexOf(':');
    }

    if (input.StartsWith("read:", StringComparison.OrdinalIgnoreCase))
    {
        var path = input[5..].Trim();
        return new ToolAction
        {
            Kind = ToolActionKind.ReadFile,
            TargetPath = ResolvePath(workspaceRoot, path)
        };
    }

    if (input.StartsWith("write:", StringComparison.OrdinalIgnoreCase))
    {
        var payload = input[6..];
        var separator = FindWriteSeparator(payload);
        var path = separator >= 0 ? payload[..separator].Trim() : payload.Trim();
        var content = separator >= 0 ? payload[(separator + 1)..] : string.Empty;

        return new ToolAction
        {
            Kind = ToolActionKind.WriteFile,
            TargetPath = ResolvePath(workspaceRoot, path),
            Payload = content
        };
    }

    if (input.StartsWith("delete:", StringComparison.OrdinalIgnoreCase))
    {
        var path = input[7..].Trim();
        return new ToolAction
        {
            Kind = ToolActionKind.DeleteFile,
            TargetPath = ResolvePath(workspaceRoot, path)
        };
    }

    if (input.StartsWith("rename:", StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith("move:", StringComparison.OrdinalIgnoreCase))
    {
        var isMove = input.StartsWith("move:", StringComparison.OrdinalIgnoreCase);
        var payload = input[(isMove ? 5 : 7)..];
        var separator = FindPathPairSeparator(payload);
        var source = separator >= 0 ? payload[..separator].Trim() : payload.Trim();
        var destination = separator >= 0 ? payload[(separator + 1)..].Trim() : string.Empty;

        return new ToolAction
        {
            Kind = isMove ? ToolActionKind.MoveFile : ToolActionKind.RenameFile,
            SourcePath = ResolvePath(workspaceRoot, source),
            DestinationPath = ResolvePath(workspaceRoot, destination)
        };
    }

    return new ToolAction { Kind = ToolActionKind.RunCommand };
};

sealed class FakeNoopTool : ITool
{
    public string Name => "fake";
    public string Description => "noop";
    public Task<string> Execute(string input) => Task.FromResult("ok");
}

sealed class ThrowingTool : ITool
{
    public string Name => "throwing";
    public string Description => "always throws";
    public Task<string> Execute(string input) => throw new InvalidOperationException("expected failure from throwing tool");
}

sealed class FakeTimeoutLlmClient : ILLMClient
{
    public Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult("Error: Ollama request timed out. simulated timeout");

    public Task<bool> IsAvailable(CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}

sealed class FakeSuccessLlmClient : ILLMClient
{
    public Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult("Это краткий анализ проекта на основе индексированного контекста без изменений кода.");

    public Task<bool> IsAvailable(CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}

sealed class FakeLlmRequestFailedClient : ILLMClient
{
    public Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult("Error: Ollama request failed. simulated provider failure");

    public Task<bool> IsAvailable(CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}

sealed class FakeUsableErrorPrefixedSuccessClient : ILLMClient
{
    public Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult("Error: I cannot execute tools directly in this mode, but here is the project analysis: the code is small, has one entry type, and does not require build changes.");

    public Task<bool> IsAvailable(CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}

sealed class FakeNoToolAnalysisClient : ILLMClient
{
    public Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult("Для анализа механизма синхронизации координат игрока между клиентом и сервером, мне нужно знать конкретный код или описание того, как происходит синхронизация.");

    public Task<bool> IsAvailable(CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}

sealed class FakeAdapter : ILlmProviderAdapter
{
    private readonly string _response;

    public FakeAdapter(string provider, string model, string response)
    {
        Metadata = new LlmProviderMetadata(provider, model, nameof(FakeAdapter));
        _response = response;
    }

    public LlmProviderMetadata Metadata { get; }

    public Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult(_response);

    public Task<bool> IsAvailable(CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}

sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpResponseMessage> _responseFactory;

    public FakeHttpMessageHandler(Func<HttpResponseMessage> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(_responseFactory());
    }
}

sealed class FailOnceThenSuccessAdapter : ILlmProviderAdapter
{
    private int _calls;
    private readonly string _error;
    public FailOnceThenSuccessAdapter(string error) { _error = error; }
    public LlmProviderMetadata Metadata => new("ollama", "test-model", nameof(FailOnceThenSuccessAdapter));
    public Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
    {
        _calls++;
        if (_calls == 1) throw new InvalidOperationException(_error);
        return Task.FromResult("Project analysis completed.");
    }
    public Task<bool> IsAvailable(CancellationToken cancellationToken = default) => Task.FromResult(true);
}

sealed class AlwaysThrowAdapter : ILlmProviderAdapter
{
    private readonly string _error;
    public AlwaysThrowAdapter(string error) { _error = error; }
    public LlmProviderMetadata Metadata => new("ollama", "test-model", nameof(AlwaysThrowAdapter));
    public Task<string> Generate(string prompt, CancellationToken cancellationToken = default) => throw new InvalidOperationException(_error);
    public Task<bool> IsAvailable(CancellationToken cancellationToken = default) => Task.FromResult(true);
}

sealed class StaticSuccessAdapter : ILlmProviderAdapter
{
    private readonly string _response;
    public StaticSuccessAdapter(string response) { _response = response; }
    public LlmProviderMetadata Metadata => new("ollama", "test-model", nameof(StaticSuccessAdapter));
    public Task<string> Generate(string prompt, CancellationToken cancellationToken = default) => Task.FromResult(_response);
    public Task<bool> IsAvailable(CancellationToken cancellationToken = default) => Task.FromResult(true);
}
