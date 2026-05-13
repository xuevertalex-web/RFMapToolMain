using System.Text.Json;
using System.Text;
using System.Diagnostics;
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
await RunConversationalTask_MutationToolCallBlockedRegression();
await RunAmbiguousGibberishTask_RequiresClarificationRegression();
await RunChatIntentConversationalResponsesRegression();
await RunClarifyIntentRequiresClarificationRegression();
await RunChatOnlyRoutingMatrix_ChatCasesRegression();
await RunChatOnlyRoutingMatrix_ClarifyCasesRegression();
await RunChatOnlyRoutingMatrix_ExecuteCasesRegression();
await RunChatOnlyRoutingMatrix_AnalysisOnlyCasesRegression();
await RunSessionFlow_ChatClarifyExecute_Regression();
await RunUnifiedIntentDecision_NaturalLanguageRegression();
await RunBroadExecuteIntent_ProjectWideFixRegression();
await RunHostDiagnosticsCommandApprovalRegression();
await RunProcessExecutionHardeningRegression();
await RunProcessArgumentListPreservesArgumentsRegression();
await RunRuntimeGpuDiagnosticsTruthfulReportingRegression();
await RunDestructiveFileApprovalMarkerRegression();
await RunGuardedToolExplicitApprovalHandoffRegression();
RunCommandRiskPolicyTokenizationRegression();
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
await RunContextSelectionEntryPointAwarenessRegression();
RunProjectMap_ClassifiesZonesAndRoles_Deterministically();
RunProjectMap_EntrypointsDetected_ByNameRules();
RunProjectMap_UnknownPaths_FallbackStable();
RunProjectRetrievalPlanner_ZoneRoleSelection();
RunProjectRetrievalPlanner_UnknownFallback();
RunRetrievalSignalScorer_TargetedRanking();
RunRetrievalSignalScorer_Determinism();
await RunRetrievalDiagnostics_TopSignalsAndVsixDeepMode();
await RunPlanningSummary_ContextAnalysisRegression();
await RunPlanningSummary_UiAnalysisRegression();
await RunPlanningSummary_UnknownFallbackRegression();
await RunPlanningSummary_ChatNoNoiseRegression();
await RunTaskPlan_AnalysisMode_NoMutationChecks();
await RunTaskPlan_ExecuteMode_InspectEditTestFlow();
await RunTaskPlan_ChatMode_NotEmitted();
await RunTaskPlan_ClarifyMode_NotEmitted();
await RunTaskPlan_CandidateFiles_MaxFive();
await RunTaskPlan_StopConditions_UnsafePathApprovalAmbiguous();
await RunTaskPlan_Checks_ByZone();
await RunContextSelection_UsesProjectMapHints_WithoutBreakingBudget();
await RunMtimeAwareIndexingCacheRegression();
RunDeepAnalysisDetectionRegression();
RunDeepAnalysisContextFormatterRegression();
RunDeepAnalysisPromptRegression();
await RunDeepAnalysisDiagnosticsRegression();
await RunAuditRoutingAndCandidateSeedingRegression();

static void RunDeepAnalysisDetectionRegression()
{
    var t = typeof(Agent).Assembly.GetType("LocalCursorAgent.Core.AnalysisPromptBuilder", throwOnError: true)!;
    var m = t.GetMethod("IsDeepAnalysisTask", new[] { typeof(string) })!;
    var eval = t.GetMethod("EvaluateDeepAnalysisTask", new[] { typeof(string) })!;
    AssertTrue((bool)m.Invoke(null, new object[] { "Find security vulnerabilities and bypasses in the workspace guard and approval model" })!, "Expected deep analysis detection for security/audit task.");
    AssertTrue((bool)m.Invoke(null, new object[] { "Проведи аудит безопасности и найди уязвимости, обходы и дыры" })!, "Expected deep analysis detection for Russian security/audit task.");
    var riskCombo = eval.Invoke(null, new object[] { "Find weak spots in workspace and command handling" })!;
    var ruRiskCombo = eval.Invoke(null, new object[] { "Где слабые места в командах, файлах и разрешениях" })!;
    var riskComboIsDeep = (bool)riskCombo.GetType().GetProperty("IsDeep")!.GetValue(riskCombo)!;
    var riskComboTrigger = (string)riskCombo.GetType().GetProperty("Trigger")!.GetValue(riskCombo)!;
    var ruRiskComboIsDeep = (bool)ruRiskCombo.GetType().GetProperty("IsDeep")!.GetValue(ruRiskCombo)!;
    var ruRiskComboTrigger = (string)ruRiskCombo.GetType().GetProperty("Trigger")!.GetValue(ruRiskCombo)!;
    AssertTrue(riskComboIsDeep && (riskComboTrigger == "risk-combination" || riskComboTrigger == "keyword"), "Expected deep trigger for weak-spots query.");
    AssertTrue(ruRiskComboIsDeep && (ruRiskComboTrigger == "risk-combination" || ruRiskComboTrigger == "keyword"), "Expected Russian deep trigger.");
    AssertTrue(!(bool)m.Invoke(null, new object[] { "Explain this project" })!, "Expected simple overview to remain non-deep analysis.");
    Console.WriteLine("PASS DeepAnalysisDetectionRegression");
}

static void RunDeepAnalysisContextFormatterRegression()
{
    var contextInfo = new ContextInformation
    {
        SelectedFiles = new List<string> { "Core/WorkspaceGuard.cs" },
        FileContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Core/WorkspaceGuard.cs"] = "public static class WorkspaceGuard { public static bool IsSafe(string path) => true; }"
        },
        RelevantSymbols = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Core/WorkspaceGuard.cs"] = new List<string> { "WorkspaceGuard", "IsSafe" }
        }
    };
    var t = typeof(Agent).Assembly.GetType("LocalCursorAgent.Core.AnalysisContextFormatter", throwOnError: true)!;
    var compact = (string)t.GetMethod("BuildCompactAnalysisContext")!.Invoke(null, new object[] { contextInfo })!;
    var deep = (string)t.GetMethod("BuildDeepAnalysisContext")!.Invoke(null, new object[] { contextInfo })!;
    AssertTrue(compact.Contains("FILE: Core/WorkspaceGuard.cs", StringComparison.Ordinal), "Expected compact context file line.");
    AssertTrue(!compact.Contains("public static class WorkspaceGuard", StringComparison.Ordinal), "Compact context should not contain file body.");
    AssertTrue(deep.Contains("// FILE: Core/WorkspaceGuard.cs", StringComparison.Ordinal), "Expected deep context file header.");
    AssertTrue(deep.Contains("public static class WorkspaceGuard", StringComparison.Ordinal), "Expected deep context to include file content.");
    Console.WriteLine("PASS DeepAnalysisContextFormatterRegression");
}

static void RunDeepAnalysisPromptRegression()
{
    var t = typeof(Agent).Assembly.GetType("LocalCursorAgent.Core.AnalysisPromptBuilder", throwOnError: true)!;
    var prompt = (string)t.GetMethod("BuildAnalysisPromptWithContext")!.Invoke(
        null,
        new object[] { "Find security vulnerabilities and bypasses in the workspace guard and approval model", 0, string.Empty, "// FILE: Core/WorkspaceGuard.cs\n```csharp\npublic class A{}\n```", "- Respond in English." })!;
    AssertTrue(prompt.Contains("This is an analysis-only task.", StringComparison.Ordinal), "Expected analysis-only guardrail.");
    AssertTrue(prompt.Contains("Do not use any tool.", StringComparison.Ordinal), "Expected no-tool guardrail.");
    AssertTrue(prompt.Contains("Distinguish confirmed issues from hypotheses.", StringComparison.Ordinal), "Expected deep-analysis prompt instructions.");
    AssertTrue(!prompt.Contains("modify files", StringComparison.OrdinalIgnoreCase), "Prompt must not instruct mutation.");
    Console.WriteLine("PASS DeepAnalysisPromptRegression");
}

static async Task RunDeepAnalysisDiagnosticsRegression()
{
    var deepStructured = await RunIntentMatrixTask("Analyze failure modes in approval tokens and file operations", new FakeNoToolAnalysisClient(), registerFileTool: true);
    var deepContext = deepStructured.GetProperty("contextDiagnostics");
    AssertTrue(deepContext.GetProperty("deepAnalysisTask").GetBoolean(), "Expected deepAnalysisTask=true for risk-combination query.");
    var deepTrigger = deepContext.GetProperty("deepAnalysisTrigger").GetString();
    AssertTrue(deepTrigger == "risk-combination" || deepTrigger == "keyword", "Expected deterministic deep trigger in diagnostics.");
    AssertTrue(deepContext.GetProperty("analysisFileBudgetCap").GetInt32() == 12, "Expected deep analysis budget cap=12.");
    AssertTrue(deepContext.GetProperty("analysisContextIncludesFileContents").GetBoolean(), "Expected deep analysis to include file contents.");
    AssertTrue(!deepContext.ToString().Contains("public static class WorkspaceGuard", StringComparison.Ordinal), "Diagnostics must not include source file contents.");

    var compactStructured = await RunIntentMatrixTask("Explain this project", new FakeNoToolAnalysisClient(), registerFileTool: true);
    var compactContext = compactStructured.GetProperty("contextDiagnostics");
    AssertTrue(!compactContext.GetProperty("deepAnalysisTask").GetBoolean(), "Expected deepAnalysisTask=false for overview query.");
    AssertTrue(compactContext.GetProperty("deepAnalysisTrigger").GetString() == "none", "Expected no deep trigger for overview query.");
    AssertTrue(compactContext.GetProperty("analysisFileBudgetCap").GetInt32() == 4, "Expected compact analysis budget cap=4.");
    AssertTrue(!compactContext.GetProperty("analysisContextIncludesFileContents").GetBoolean(), "Expected compact analysis not to include file contents.");
    Console.WriteLine("PASS DeepAnalysisDiagnosticsRegression");
}

static async Task RunAuditRoutingAndCandidateSeedingRegression()
{
    var cases = new (string Task, string[] ExpectedSeeds)[]
    {
        ("Find bypasses in approval tokens and destructive file operations", new[] { "Tools/FileTool.cs", "SafetyTests/Program.cs" }),
        ("Find weak spots in command execution and shell handling", new[] { "Execution/SafeProcessRunner.cs", "Security/CommandRiskPolicy.cs" }),
        ("Что можно обойти в workspace guard и approval tokens", new[] { "vscode-extension/workspaceResolver.js", "vscode-extension/workspaceTaskClassifier.js" }),
        ("Find stale VSIX/install workflow risks", new[] { "scripts/devtools/Update-VSCodeExtension.cmd", "vscode-extension/package.json" }),
        ("Find retrieval/context blind spots in deep analysis mode", new[] { "Core/Agent.ContextPreparation.cs", "Context/ContextBuilder.cs" })
    };

    foreach (var c in cases)
    {
        var structured = await RunIntentMatrixTask(c.Task, new FakeNoToolAnalysisClient(), registerFileTool: true);
        var reason = structured.GetProperty("reasonCode").GetString() ?? string.Empty;
        AssertTrue(!string.Equals(reason, "CLARIFICATION_REQUIRED", StringComparison.OrdinalIgnoreCase), $"Audit query should not route to clarification: {c.Task}");
        AssertTrue(!string.Equals(reason, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase), $"Audit query should not end as chat fast-path: {c.Task}");
        AssertTrue(!string.Equals(reason, "TARGET_RESOLUTION_FAILED", StringComparison.OrdinalIgnoreCase), $"Audit query should not fail target-resolution gate: {c.Task}");
        var context = structured.GetProperty("contextDiagnostics");
        AssertTrue(context.GetProperty("deepAnalysisTask").GetBoolean(), $"Audit query should activate deep analysis: {c.Task}");
        var seedCategory = context.GetProperty("candidateSeedCategory").GetString() ?? string.Empty;
        AssertTrue(!string.Equals(seedCategory, "none", StringComparison.OrdinalIgnoreCase), $"Expected candidate seed category for query: {c.Task}");
        AssertTrue(context.GetProperty("auditAnalysisRouting").GetBoolean(), $"Expected auditAnalysisRouting=true: {c.Task}");
        AssertTrue(context.GetProperty("bypassedFastPath").GetBoolean(), $"Expected bypassedFastPath=true: {c.Task}");
        var seeded = context.GetProperty("seededCandidateFiles").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
        AssertTrue(seeded.Length <= 5, "Expected compact seededCandidateFiles diagnostics.");
        AssertTrue(structured.GetProperty("retrievalPlanningDiagnostics").ValueKind == JsonValueKind.Object, "Expected retrieval diagnostics object.");
    }

    var unrelated = await RunIntentMatrixTask("Explain this project", new FakeNoToolAnalysisClient(), registerFileTool: true);
    var unrelatedContext = unrelated.GetProperty("contextDiagnostics");
    AssertTrue(string.Equals(unrelatedContext.GetProperty("candidateSeedCategory").GetString(), "none", StringComparison.OrdinalIgnoreCase) || unrelatedContext.GetProperty("seededCandidateFiles").GetArrayLength() == 0, "Unrelated query should not get audit seeds.");
    Console.WriteLine("PASS AuditRoutingAndCandidateSeedingRegression");
}

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
    AssertTrue(structured.TryGetProperty("projectMapDiagnostics", out var projectMapDiagnostics), "Expected projectMapDiagnostics in payload.");
    AssertTrue(projectMapDiagnostics.GetProperty("enabled").GetBoolean(), "Expected projectMapDiagnostics.enabled=true.");
    AssertTrue(projectMapDiagnostics.GetProperty("fileCount").GetInt32() > 0, "Expected projectMapDiagnostics.fileCount > 0.");
    AssertTrue(projectMapDiagnostics.GetProperty("zoneCounts").ValueKind == JsonValueKind.Object, "Expected zoneCounts object.");
    AssertTrue(projectMapDiagnostics.GetProperty("roleCounts").ValueKind == JsonValueKind.Object, "Expected roleCounts object.");
    AssertTrue(projectMapDiagnostics.TryGetProperty("rulesVersion", out var rulesVersion) && !string.IsNullOrWhiteSpace(rulesVersion.GetString()), "Expected non-empty rulesVersion.");
    AssertTrue(!projectMapDiagnostics.TryGetProperty("files", out _), "Expected compact projectMapDiagnostics without files list.");
    AssertTrue(structured.TryGetProperty("retrievalPlanningDiagnostics", out var retrievalPlanningDiagnostics), "Expected retrievalPlanningDiagnostics in payload.");
    AssertTrue(retrievalPlanningDiagnostics.GetProperty("selectedZones").ValueKind == JsonValueKind.Array, "Expected selectedZones array.");
    AssertTrue(retrievalPlanningDiagnostics.GetProperty("selectedRoles").ValueKind == JsonValueKind.Array, "Expected selectedRoles array.");
    AssertTrue(retrievalPlanningDiagnostics.GetProperty("confidence").GetDouble() >= 0.0, "Expected confidence >= 0.");

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
    var (structured, _, runtimeRoot) = await RunAgentWithAdapter(new FailOnceThenSuccessAdapter("Error: request timed out"));
    AssertTrue(structured.GetProperty("ok").GetBoolean(), "Expected success after retry.");
    AssertTrue(structured.GetProperty("retryCount").GetInt32() > 0, "Expected retryCount > 0.");
    var retryDiagnostics = structured.GetProperty("retryDiagnostics");
    var retryAttemptsPayload = retryDiagnostics.GetProperty("attempts");
    AssertTrue(retryAttemptsPayload.ValueKind == JsonValueKind.Array && retryAttemptsPayload.GetArrayLength() >= 1, "Expected retryDiagnostics.attempts for success-after-retry.");
    var retryEvents = LoadRetryAttemptEvents(runtimeRoot);
    AssertTrue(retryEvents.Count >= 1, "Expected retry attempt diagnostics events.");
    var first = retryEvents[0];
    var firstMetadata = GetCaseInsensitiveProperty(first, "metadata");
    AssertTrue(GetCaseInsensitiveProperty(firstMetadata, "attempt").GetInt32() == 1, "Expected retry attempt=1.");
    AssertTrue(GetCaseInsensitiveProperty(firstMetadata, "will_retry").GetBoolean(), "Expected will_retry=true for first failed attempt.");
    AssertTrue(!GetCaseInsensitiveProperty(firstMetadata, "final_attempt").GetBoolean(), "Expected final_attempt=false for first failed attempt.");
    AssertTrue(GetCaseInsensitiveProperty(firstMetadata, "delay_ms").GetInt32() > 0, "Expected positive retry delay.");
    Console.WriteLine("PASS LlmRetry_SuccessAfterRetry");
}

static async Task RunLlmRetryFailRegression()
{
    var (structured, _, runtimeRoot) = await RunAgentWithAdapter(new AlwaysThrowAdapter("429 rate limit"));
    AssertTrue(!structured.GetProperty("ok").GetBoolean(), "Expected failure after retry exhaustion.");
    AssertTrue(structured.GetProperty("retryCount").GetInt32() > 0, "Expected retryCount > 0 for fail path.");
    var errorType = structured.GetProperty("errorType").GetString() ?? string.Empty;
    AssertTrue(string.Equals(errorType, "provider_rate_limit", StringComparison.Ordinal), $"Expected provider_rate_limit, got {errorType}.");
    var retryDiagnostics = structured.GetProperty("retryDiagnostics");
    var retryAttemptsPayload = retryDiagnostics.GetProperty("attempts");
    AssertTrue(retryAttemptsPayload.ValueKind == JsonValueKind.Array && retryAttemptsPayload.GetArrayLength() == 3, "Expected three retryDiagnostics attempts for fail-after-retries.");
    var retryEvents = LoadRetryAttemptEvents(runtimeRoot);
    AssertTrue(retryEvents.Count == 3, "Expected retry diagnostics for all 3 failed attempts.");
    var last = retryEvents[^1];
    var lastMetadata = GetCaseInsensitiveProperty(last, "metadata");
    AssertTrue(GetCaseInsensitiveProperty(lastMetadata, "final_attempt").GetBoolean(), "Expected final_attempt=true for last failed attempt.");
    AssertTrue(!GetCaseInsensitiveProperty(lastMetadata, "will_retry").GetBoolean(), "Expected will_retry=false for last failed attempt.");
    Console.WriteLine("PASS LlmRetry_FailAfterRetries");
}

static async Task RunLlmRetryNoRetryRegression()
{
    var (structured, _, runtimeRoot) = await RunAgentWithAdapter(new StaticSuccessAdapter("analysis success"));
    AssertTrue(structured.GetProperty("ok").GetBoolean(), "Expected first-attempt success.");
    AssertTrue(structured.GetProperty("retryCount").GetInt32() == 0, "Expected retryCount=0 without retries.");
    AssertTrue(structured.TryGetProperty("indexingDiagnostics", out var indexingDiagnostics), "Expected indexingDiagnostics in payload.");
    AssertTrue(indexingDiagnostics.GetProperty("indexedFiles").GetInt32() >= 1, "Expected indexedFiles >= 1.");
    AssertTrue(indexingDiagnostics.GetProperty("cacheHits").GetInt32() >= 0, "Expected cacheHits >= 0.");
    AssertTrue(indexingDiagnostics.GetProperty("cacheMisses").GetInt32() >= 0, "Expected cacheMisses >= 0.");
    var retryDiagnostics = structured.GetProperty("retryDiagnostics");
    var retryAttemptsPayload = retryDiagnostics.GetProperty("attempts");
    AssertTrue(retryAttemptsPayload.ValueKind == JsonValueKind.Array && retryAttemptsPayload.GetArrayLength() == 0, "Expected no retryDiagnostics attempts on no-retry success.");
    var retryEvents = LoadRetryAttemptEvents(runtimeRoot);
    AssertTrue(retryEvents.Count == 0, "Expected no retry attempt diagnostics for no-retry success.");
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

static List<JsonElement> LoadRetryAttemptEvents(string runtimeRoot)
{
    var runsDir = Path.Combine(runtimeRoot, "logs", "machine", "runs");
    var eventsFile = Directory.GetFiles(runsDir, "*.events.jsonl")
        .Select(path => new FileInfo(path))
        .OrderByDescending(f => f.LastWriteTimeUtc)
        .FirstOrDefault()?.FullName;
    AssertTrue(!string.IsNullOrWhiteSpace(eventsFile), "Expected events jsonl file for run.");

    var items = new List<JsonElement>();
    foreach (var line in File.ReadLines(eventsFile!))
    {
        if (string.IsNullOrWhiteSpace(line))
            continue;
        using var doc = JsonDocument.Parse(line);
        if (TryGetCaseInsensitiveProperty(doc.RootElement, "eventType", out var eventType) &&
            string.Equals(eventType.GetString(), "ModelRetryAttempt", StringComparison.Ordinal))
            items.Add(doc.RootElement.Clone());
    }

    return items;
}

static bool TryGetCaseInsensitiveProperty(JsonElement element, string propertyName, out JsonElement property)
{
    if (element.TryGetProperty(propertyName, out property))
        return true;

    foreach (var candidate in element.EnumerateObject())
    {
        if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
        {
            property = candidate.Value;
            return true;
        }
    }

    property = default;
    return false;
}

static JsonElement GetCaseInsensitiveProperty(JsonElement element, string propertyName)
{
    AssertTrue(TryGetCaseInsensitiveProperty(element, propertyName, out var property), $"Expected property '{propertyName}'.");
    return property;
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

static async Task RunConversationalTask_MutationToolCallBlockedRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    File.WriteAllText(Path.Combine(workspaceRoot, "notes.txt"), "seed");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun("а щас", "а щас", workspaceRoot, runtimeRoot, AgentAccessMode.WorkspaceWrite.ToString(), "FakeMutationToolCallClient", "fake-mutation-tool-model");

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
    var patchGate = new PatchSafetyGate(session, permissionGuard, tracer);
    var destructiveGate = new DestructiveOperationSafetyGate(session, permissionGuard, tracer);
    toolRegistry.Register(new FileTool(session, permissionGuard, patchGate, destructiveGate, sandboxManager, tracer));
    var embeddingService = new EmbeddingService(disabled: true);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var projectIndexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);
    var contextBuilder = new ContextBuilder(workspaceRoot, vectorStore, fileStateManager, new ProjectSymbolDirectory(), tracer);

    var agent = new Agent(
        new FakeMutationToolCallClient(),
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
    try { _ = await agent.RunTask("а щас"); }
    finally { Console.SetOut(oldOut); }

    var structured = ExtractStructuredPayload(capture.ToString());
    AssertTrue(structured.GetProperty("ok").GetBoolean(), "Expected conversational task to complete successfully.");
    AssertTrue(string.Equals(structured.GetProperty("reasonCode").GetString(), "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase), "Expected SUCCESS_NO_TOOL_CALLS.");
    AssertTrue(structured.GetProperty("changedFiles").GetArrayLength() == 0, "Expected no changed files.");
    AssertTrue(!File.Exists(Path.Combine(workspaceRoot, "Calculator.cs")), "Expected Calculator.cs to not be created.");
    Console.WriteLine("PASS ConversationalTask_MutationToolCallBlocked");
}

static async Task RunAmbiguousGibberishTask_RequiresClarificationRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun("кйцуйцщутщ", "кйцуйцщутщ", workspaceRoot, runtimeRoot, AgentAccessMode.WorkspaceWrite.ToString(), "FakeNoToolAnalysisClient", "fake-no-tool-model");

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
    try { _ = await agent.RunTask("кйцуйцщутщ"); }
    finally { Console.SetOut(oldOut); }

    var structured = ExtractStructuredPayload(capture.ToString());
    AssertTrue(!structured.GetProperty("ok").GetBoolean(), "Expected ambiguous gibberish to require clarification.");
    AssertTrue(string.Equals(structured.GetProperty("reasonCode").GetString(), "CLARIFICATION_REQUIRED", StringComparison.OrdinalIgnoreCase), "Expected CLARIFICATION_REQUIRED.");
    AssertTrue(structured.GetProperty("changedFiles").GetArrayLength() == 0, "Expected no changed files.");
    Console.WriteLine("PASS AmbiguousGibberishTask_RequiresClarification");
}

static async Task RunChatIntentConversationalResponsesRegression()
{
    foreach (var input in new[] { "привет", "что ты умеешь?", "объясни что делает проект" })
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
        var workspaceRoot = Path.Combine(tempRoot, "workspace");
        var runtimeRoot = Path.Combine(tempRoot, "runtime");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(runtimeRoot);

        var tracer = new ExecutionTracer(runtimeRoot);
        tracer.StartRun(input, input, workspaceRoot, runtimeRoot, AgentAccessMode.WorkspaceWrite.ToString(), "FakeNoToolAnalysisClient", "fake-chat-model");
        var session = new AgentSessionContext
        {
            SessionId = Guid.NewGuid().ToString("N"),
            RuntimeRoot = runtimeRoot,
            ActiveWorkspaceRoot = workspaceRoot,
            AccessMode = AgentAccessMode.WorkspaceWrite,
            ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
        };

        var agent = CreateSimpleAgentForIntentTests(workspaceRoot, runtimeRoot, session, tracer, new FakeNoToolAnalysisClient());
        var oldOut = Console.Out;
        var capture = new StringWriter();
        Console.SetOut(capture);
        try { _ = await agent.RunTask(input); } finally { Console.SetOut(oldOut); }

        var structured = ExtractStructuredPayload(capture.ToString());
        AssertTrue(structured.GetProperty("ok").GetBoolean(), "Expected chat intent success.");
        var reasonCode = structured.GetProperty("reasonCode").GetString() ?? string.Empty;
        AssertTrue(
            string.Equals(reasonCode, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(reasonCode, "SUCCESS_ANALYSIS_RESPONSE", StringComparison.OrdinalIgnoreCase),
            "Expected safe no-tool chat/analysis outcome for chat intent.");
        AssertTrue(structured.GetProperty("changedFiles").GetArrayLength() == 0, "Expected no file changes for chat.");
        AssertTrue(!string.IsNullOrWhiteSpace(structured.GetProperty("message").GetString()), "Expected assistant response message.");
    }

    Console.WriteLine("PASS ChatIntent_ConversationalResponses");
}

static async Task RunClarifyIntentRequiresClarificationRegression()
{
    foreach (var input in new[] { "сделай нормально", "оно не работает", "абракадабра" })
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
        var workspaceRoot = Path.Combine(tempRoot, "workspace");
        var runtimeRoot = Path.Combine(tempRoot, "runtime");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(runtimeRoot);

        var tracer = new ExecutionTracer(runtimeRoot);
        tracer.StartRun(input, input, workspaceRoot, runtimeRoot, AgentAccessMode.WorkspaceWrite.ToString(), "FakeNoToolAnalysisClient", "fake-clarify-model");
        var session = new AgentSessionContext
        {
            SessionId = Guid.NewGuid().ToString("N"),
            RuntimeRoot = runtimeRoot,
            ActiveWorkspaceRoot = workspaceRoot,
            AccessMode = AgentAccessMode.WorkspaceWrite,
            ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
        };

        var agent = CreateSimpleAgentForIntentTests(workspaceRoot, runtimeRoot, session, tracer, new FakeNoToolAnalysisClient());
        var oldOut = Console.Out;
        var capture = new StringWriter();
        Console.SetOut(capture);
        try { _ = await agent.RunTask(input); } finally { Console.SetOut(oldOut); }

        var structured = ExtractStructuredPayload(capture.ToString());
        AssertTrue(!structured.GetProperty("ok").GetBoolean(), "Expected clarify intent to require clarification.");
        AssertTrue(string.Equals(structured.GetProperty("reasonCode").GetString(), "CLARIFICATION_REQUIRED", StringComparison.OrdinalIgnoreCase), "Expected CLARIFICATION_REQUIRED.");
        AssertTrue(structured.GetProperty("changedFiles").GetArrayLength() == 0, "Expected no file changes for clarify.");
        var message = structured.GetProperty("message").GetString() ?? string.Empty;
        AssertTrue(!string.IsNullOrWhiteSpace(message), "Expected non-empty clarification message.");
    }

    Console.WriteLine("PASS ClarifyIntent_RequiresClarification");
}

static async Task RunChatOnlyRoutingMatrix_ChatCasesRegression()
{
    var inputs = new[]
    {
        "привет",
        "как дела?",
        "что ты умеешь?",
        "объясни что делает этот проект",
        "расскажи как работает ContextBuilder",
        "какие риски у текущей архитектуры?",
        "что дальше лучше сделать?"
    };

    foreach (var input in inputs)
    {
        var structured = await RunIntentMatrixTask(input, new FakeNoToolAnalysisClient(), registerFileTool: true);
        AssertTrue(structured.GetProperty("changedFiles").GetArrayLength() == 0, $"Expected no file changes for chat phrase '{input}'.");
        var reasonCode = structured.GetProperty("reasonCode").GetString() ?? string.Empty;
        AssertTrue(
            string.Equals(reasonCode, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(reasonCode, "SUCCESS_ANALYSIS_RESPONSE", StringComparison.OrdinalIgnoreCase),
            $"Expected chat-safe reason for '{input}', got '{reasonCode}'.");
    }

    Console.WriteLine("PASS ChatOnlyRoutingMatrix_ChatCases");
}

static async Task RunChatOnlyRoutingMatrix_ClarifyCasesRegression()
{
    var inputs = new[]
    {
        "сделай нормально",
        "почини",
        "оно не работает",
        "разберись",
        "улучши всё"
    };

    foreach (var input in inputs)
    {
        var structured = await RunIntentMatrixTask(input, new FakeNoToolAnalysisClient(), registerFileTool: true);
        AssertTrue(!structured.GetProperty("ok").GetBoolean(), $"Expected clarify outcome for '{input}'.");
        AssertTrue(string.Equals(structured.GetProperty("reasonCode").GetString(), "CLARIFICATION_REQUIRED", StringComparison.OrdinalIgnoreCase), $"Expected CLARIFICATION_REQUIRED for '{input}'.");
        AssertTrue(structured.GetProperty("changedFiles").GetArrayLength() == 0, $"Expected no file changes for ambiguous phrase '{input}'.");
    }

    Console.WriteLine("PASS ChatOnlyRoutingMatrix_ClarifyCases");
}

static async Task RunChatOnlyRoutingMatrix_ExecuteCasesRegression()
{
    var inputs = new[]
    {
        "создай файл README_TEST.md с текстом hello",
        "добавь тест для PermissionGuard",
        "исправь ошибку в ContextBuilder.cs",
        "обнови package.json script test",
        "удали временный файл temp.txt"
    };

    foreach (var input in inputs)
    {
        var structured = await RunIntentMatrixTask(input, new FakeNoToolAnalysisClient(), registerFileTool: true);
        var reasonCode = structured.GetProperty("reasonCode").GetString() ?? string.Empty;
        AssertTrue(!string.Equals(reasonCode, "CLARIFICATION_REQUIRED", StringComparison.OrdinalIgnoreCase), $"Execute phrase '{input}' was incorrectly routed to clarification.");
        AssertTrue(!string.Equals(reasonCode, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase), $"Execute phrase '{input}' was incorrectly routed to chat.");
    }

    Console.WriteLine("PASS ChatOnlyRoutingMatrix_ExecuteCases");
}

static async Task RunChatOnlyRoutingMatrix_AnalysisOnlyCasesRegression()
{
    var inputs = new[]
    {
        "проанализируй ContextBuilder.cs и скажи что не так, не меняй файлы",
        "сделай code review без правок",
        "объясни diff без изменений"
    };

    foreach (var input in inputs)
    {
        var structured = await RunIntentMatrixTask(input, new FakeNoToolAnalysisClient(), registerFileTool: true);
        AssertTrue(structured.GetProperty("changedFiles").GetArrayLength() == 0, $"Expected no file changes for analysis-only phrase '{input}'.");
        var reasonCode = structured.GetProperty("reasonCode").GetString() ?? string.Empty;
        AssertTrue(
            string.Equals(reasonCode, "SUCCESS_ANALYSIS_RESPONSE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(reasonCode, "ANALYSIS_FALLBACK_USED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(reasonCode, "NO_ACTIONABLE_STEPS", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(reasonCode, "MAX_ITERATIONS_REACHED", StringComparison.OrdinalIgnoreCase),
            $"Expected analysis-safe reason for '{input}', got '{reasonCode}'.");
    }

    Console.WriteLine("PASS ChatOnlyRoutingMatrix_AnalysisOnlyCases");
}

static async Task RunBroadExecuteIntent_ProjectWideFixRegression()
{
    var inputs = new[]
    {
        "найди ошибку в проекте и исправь",
        "почини проект",
        "исправь что сломано в проекте",
        "устрани проблему в проекте",
        "разберись и исправь баг",
        "найди ошибку и сделай фикс",
        "запусти doctor и почини что упадет",
        "добавь preflight перед run в extension",
        "проверь health-check и исправь проблему",
        "добавь regression для intent classifier",
        "расшири intent словарь новыми командами",
        "сними тупик clarification required в ui",
        "добавь примеры как формулировать задачу",
        "перед run проверь backendProjectPath",
        "добавь repository в package.json extension",
        "прогони smoke и npm test и поправь ошибки"
    };

    foreach (var input in inputs)
    {
        var structured = await RunIntentMatrixTask(input, new FakeNoToolAnalysisClient(), registerFileTool: true);
        var reasonCode = structured.GetProperty("reasonCode").GetString() ?? string.Empty;
        AssertTrue(!string.Equals(reasonCode, "CLARIFICATION_REQUIRED", StringComparison.OrdinalIgnoreCase), $"Broad execute phrase '{input}' was incorrectly routed to clarification.");
        AssertTrue(!string.Equals(reasonCode, "NON_ACTIONABLE_TASK", StringComparison.OrdinalIgnoreCase), $"Broad execute phrase '{input}' was incorrectly routed to non-actionable.");
        AssertTrue(!string.Equals(reasonCode, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase), $"Broad execute phrase '{input}' was incorrectly routed to chat.");
    }

    Console.WriteLine("PASS BroadExecuteIntent_ProjectWideFix");
}

static async Task RunSessionFlow_ChatClarifyExecute_Regression()
{
    var chat = await RunIntentMatrixTask("привет, что ты умеешь?", new FakeNoToolAnalysisClient(), registerFileTool: true);
    AssertTrue(chat.GetProperty("changedFiles").GetArrayLength() == 0, "Chat step must not change files.");
    var chatReason = chat.GetProperty("reasonCode").GetString() ?? string.Empty;
    AssertTrue(
        string.Equals(chatReason, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(chatReason, "SUCCESS_ANALYSIS_RESPONSE", StringComparison.OrdinalIgnoreCase),
        "Chat step must stay conversational.");

    var clarify = await RunIntentMatrixTask("сделай нормально", new FakeNoToolAnalysisClient(), registerFileTool: true);
    AssertTrue(!clarify.GetProperty("ok").GetBoolean(), "Clarify step must be non-success.");
    AssertTrue(string.Equals(clarify.GetProperty("reasonCode").GetString(), "CLARIFICATION_REQUIRED", StringComparison.OrdinalIgnoreCase), "Clarify step must require clarification.");
    AssertTrue(clarify.GetProperty("changedFiles").GetArrayLength() == 0, "Clarify step must not change files.");

    var execute = await RunIntentMatrixTask("создай файл AGENT_SESSION_E2E.md с текстом hello", new FakeNoToolAnalysisClient(), registerFileTool: true);
    var executeReason = execute.GetProperty("reasonCode").GetString() ?? string.Empty;
    AssertTrue(!string.Equals(executeReason, "CLARIFICATION_REQUIRED", StringComparison.OrdinalIgnoreCase), "Execute step must not fallback to clarification.");
    AssertTrue(!string.Equals(executeReason, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase), "Execute step must not fallback to chat.");

    Console.WriteLine("PASS SessionFlow_ChatClarifyExecute");
}

static async Task RunUnifiedIntentDecision_NaturalLanguageRegression()
{
    foreach (var input in new[] { "ну что думаешь по проекту?", "как лучше это доделать?", "объясни что тут происходит" })
    {
        var structured = await RunIntentMatrixTask(input, new FakeNoToolAnalysisClient(), registerFileTool: true);
        var reasonCode = structured.GetProperty("reasonCode").GetString() ?? string.Empty;
        AssertTrue(
            string.Equals(reasonCode, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(reasonCode, "SUCCESS_ANALYSIS_RESPONSE", StringComparison.OrdinalIgnoreCase),
            $"Expected chat/analysis outcome for '{input}', got '{reasonCode}'.");
        AssertTrue(structured.GetProperty("changedFiles").GetArrayLength() == 0, $"Expected no file changes for '{input}'.");
    }

    foreach (var input in new[] { "посмотри код и скажи что не так, без правок", "сделай code review, ничего не меняй" })
    {
        var structured = await RunIntentMatrixTask(input, new FakeNoToolAnalysisClient(), registerFileTool: true);
        AssertTrue(structured.GetProperty("changedFiles").GetArrayLength() == 0, $"Expected analysis-only no changes for '{input}'.");
        var reasonCode = structured.GetProperty("reasonCode").GetString() ?? string.Empty;
        AssertTrue(!string.Equals(reasonCode, "CLARIFICATION_REQUIRED", StringComparison.OrdinalIgnoreCase), $"Analysis-only phrase '{input}' should not require clarification.");
    }

    foreach (var input in new[] { "сделай нормально", "оно не работает" })
    {
        var structured = await RunIntentMatrixTask(input, new FakeNoToolAnalysisClient(), registerFileTool: true);
        AssertTrue(string.Equals(structured.GetProperty("reasonCode").GetString(), "CLARIFICATION_REQUIRED", StringComparison.OrdinalIgnoreCase), $"Expected clarification for '{input}'.");
        AssertTrue(structured.GetProperty("changedFiles").GetArrayLength() == 0, $"Expected no file changes for clarify phrase '{input}'.");
    }

    foreach (var input in new[] { "создай файл X", "исправь ошибку в Y", "добавь тест", "удали временный файл" })
    {
        var structured = await RunIntentMatrixTask(input, new FakeNoToolAnalysisClient(), registerFileTool: true);
        var reasonCode = structured.GetProperty("reasonCode").GetString() ?? string.Empty;
        AssertTrue(!string.Equals(reasonCode, "CLARIFICATION_REQUIRED", StringComparison.OrdinalIgnoreCase), $"Execute phrase '{input}' should not require clarification.");
        AssertTrue(!string.Equals(reasonCode, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase), $"Execute phrase '{input}' should not be chat-routed.");
    }

    Console.WriteLine("PASS UnifiedIntentDecision_NaturalLanguage");
}

static async Task<JsonElement> RunIntentMatrixTask(string task, ILLMClient llmClient, bool registerFileTool)
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    File.WriteAllText(Path.Combine(workspaceRoot, "seed.txt"), "seed");
    File.WriteAllText(Path.Combine(workspaceRoot, "ContextBuilder.cs"), "public static class ContextBuilder { }");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun(task, task, workspaceRoot, runtimeRoot, AgentAccessMode.WorkspaceWrite.ToString(), "IntentMatrixClient", "intent-matrix-model");

    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };

    var toolRegistry = new ToolRegistry();
    var permissionGuard = new PermissionGuard();
    var sandboxManager = new SandboxManager(workspaceRoot, runtimeRoot);
    if (registerFileTool)
    {
        var patchGate = new PatchSafetyGate(session, permissionGuard, tracer);
        var destructiveGate = new DestructiveOperationSafetyGate(session, permissionGuard, tracer);
        toolRegistry.Register(new FileTool(session, permissionGuard, patchGate, destructiveGate, sandboxManager, tracer));
    }

    var memory = new MemoryStore();
    var safeProcessRunner = new SafeProcessRunner(session, permissionGuard, tracer);
    var buildVerifier = new BuildVerifier(safeProcessRunner, tracer);
    var embeddingService = new EmbeddingService(disabled: true);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var projectIndexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);
    var contextBuilder = new ContextBuilder(workspaceRoot, vectorStore, fileStateManager, new ProjectSymbolDirectory(), tracer);

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
    try { _ = await agent.RunTask(task); } finally { Console.SetOut(oldOut); }
    return ExtractStructuredPayload(capture.ToString());
}

static Agent CreateSimpleAgentForIntentTests(string workspaceRoot, string runtimeRoot, AgentSessionContext session, ExecutionTracer tracer, ILLMClient llmClient)
{
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

    return new Agent(
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

static async Task RunProcessArgumentListPreservesArgumentsRegression()
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
    var runner = new SafeProcessRunner(session, guard);
    var probeScript = Path.Combine(workspaceRoot, "probe-args.ps1");
    await File.WriteAllTextAsync(
        probeScript,
        "param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Rest)\n$Rest | ForEach-Object { Write-Output $_ }\n",
        Encoding.UTF8);

    var args = new[]
    {
        "-NoProfile",
        "-File",
        probeScript,
        "simple",
        "two words",
        @"C:\\Temp\\Folder With Spaces\\",
        "value \"quoted\" here",
        @"ends-with-backslash\\",
        "mix \\\" quote and slash\\"
    };

    var result = await runner.RunAsync(new SafeProcessRequest
    {
        Kind = ToolActionKind.RunCommand,
        Command = "powershell",
        Args = args,
        WorkingDirectory = workspaceRoot,
        Timeout = TimeSpan.FromSeconds(15)
    });

    AssertTrue(!result.Success && result.ReasonCode == "INVALID_COMMAND", "Expected powershell command to remain blocked by allowlist.");

    // Validate exact argument preservation by probing ProcessStartInfo.ArgumentList directly.
    var startInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        WorkingDirectory = workspaceRoot,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    foreach (var arg in args)
        startInfo.ArgumentList.Add(arg);
    AssertTrue(startInfo.ArgumentList.Count == args.Length, "Expected argument count preservation in ArgumentList.");
    for (var i = 0; i < args.Length; i++)
        AssertTrue(string.Equals(startInfo.ArgumentList[i], args[i], StringComparison.Ordinal), $"Argument mismatch at index {i}.");

    Console.WriteLine("PASS ProcessArgumentList_PreservesArguments");
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

static void RunCommandRiskPolicyTokenizationRegression()
{
    var t = typeof(Agent).Assembly.GetType("LocalCursorAgent.Security.CommandRiskPolicy", throwOnError: true)!;
    var isHighRisk = t.GetMethod("IsHighRiskCommand", new[] { typeof(string) })!;
    var resolveRisk = t.GetMethod("ResolveCommandRiskLevel", new[] { typeof(string) })!;
    bool High(string command) => (bool)isHighRisk.Invoke(null, new object[] { command })!;
    string Risk(string command) => (string)resolveRisk.Invoke(null, new object[] { command })!;

    AssertTrue(!High("dotnet --version"), "Expected safe baseline command to remain non-high-risk.");
    AssertTrue(High("ri TEST.md"), "Expected Remove-Item alias to be high-risk.");
    AssertTrue(High("rd /s /q temp"), "Expected rd /s /q to be high-risk.");
    AssertTrue(High("erase /q temp.txt"), "Expected erase alias to be high-risk.");
    AssertTrue(High("git reset --hard"), "Expected destructive git reset to be high-risk.");
    AssertTrue(High("git clean -fd"), "Expected destructive git clean to be high-risk.");
    AssertTrue(High("git checkout -- ."), "Expected destructive git checkout to be high-risk.");
    AssertTrue(High("git restore ."), "Expected destructive git restore to be high-risk.");
    AssertTrue(High("powershell -EncodedCommand SQBlAHgA"), "Expected encoded PowerShell command to be high-risk.");
    AssertTrue(High("npm run dangerous-script"), "Expected npm run script execution to be high-risk.");
    AssertTrue(High("echo ok && whoami"), "Expected chained command to be high-risk.");
    AssertTrue(Risk("rm -rf /tmp/test") == "high", "Expected rm -rf to resolve as high.");
    AssertTrue(Risk("powershell -enc SQBlAHgA") == "high", "Expected encoded PowerShell to resolve as high.");
    AssertTrue(Risk("git reset --hard") == "high", "Expected destructive git reset to resolve as high.");
    Console.WriteLine("PASS RunCommandRiskPolicy_TokenizationRegression");
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

static async Task RunContextSelectionEntryPointAwarenessRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);

    // dotnet entry points
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "Program.cs"), new string('p', 1200));
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "Sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "Service.cs"), new string('s', 50000));

    // node entry point
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "package.json"), "{ \"name\": \"sample\" }");
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "index.js"), "console.log('x');");

    var tracer = new ExecutionTracer(runtimeRoot);
    var builder = new ContextBuilder(workspaceRoot, new VectorStore(), new FileStateManager(), new ProjectSymbolDirectory(), tracer);
    var semantic = new List<string> { "Service.cs", "Program.cs", "Sample.csproj", "package.json", "index.js" };
    var symbols = new List<string> { "Service.cs" };

    var info = builder.BuildContext("create dotnet console from scratch", semantic, symbols, 3);
    AssertTrue(info.SelectedFiles.Contains("Program.cs", StringComparer.OrdinalIgnoreCase), "Expected Program.cs entry point in context.");
    AssertTrue(info.SelectedFiles.Any(x => x.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)), "Expected .csproj entry point in context.");
    AssertTrue(info.SelectedFiles.Contains("package.json", StringComparer.OrdinalIgnoreCase), "Expected package.json entry point in context.");

    var diagnostics = ContextBuilder.GetLatestDiagnostics();
    AssertTrue(diagnostics.Items.Any(x => x.Path.Equals("Program.cs", StringComparison.OrdinalIgnoreCase) && x.Reason == "entry-point"), "Expected Program.cs entry-point reason.");
    AssertTrue(diagnostics.Items.Any(x => x.Path.Equals("Sample.csproj", StringComparison.OrdinalIgnoreCase) && x.Reason == "entry-point"), "Expected .csproj entry-point reason.");
    AssertTrue(diagnostics.Items.Any(x => x.Path.Equals("package.json", StringComparison.OrdinalIgnoreCase) && x.Reason == "entry-point"), "Expected package.json entry-point reason.");

    var nodeInfo = builder.BuildContext("create node app", semantic, symbols, 2);
    AssertTrue(nodeInfo.SelectedFiles.Contains("package.json", StringComparer.OrdinalIgnoreCase), "Expected package.json retained under tight budget.");

    Console.WriteLine("PASS ContextSelection_EntryPointAwareness");
}

static async Task RunMtimeAwareIndexingCacheRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(Path.Combine(workspaceRoot, "Core"));

    var fileA = Path.Combine(workspaceRoot, "Core", "AService.cs");
    var fileB = Path.Combine(workspaceRoot, "Core", "BService.cs");
    await File.WriteAllTextAsync(fileA, "public class AService { public void RunA() {} }");
    await File.WriteAllTextAsync(fileB, "public class BService { public void RunB() {} }");

    var embeddingService = new EmbeddingService(disabled: true);
    var vectorStore = new VectorStore();
    var fileStateManager = new FileStateManager();
    var indexer = new ProjectIndexer(workspaceRoot, embeddingService, vectorStore, new AgentConfig(workspaceRoot), fileStateManager);

    var first = await indexer.IndexProject();
    AssertTrue(first.Success, "Expected first index pass success.");
    AssertTrue(indexer.CacheHits == 0, "Expected no cache hits on cold index.");
    AssertTrue(indexer.CacheMisses >= 2, "Expected cache misses for initial indexed files.");
    var relA = Path.Combine("Core", "AService.cs");
    var relB = Path.Combine("Core", "BService.cs");
    var symbolsA1 = indexer.SymbolDirectory.GetSymbols(relA);
    AssertTrue(symbolsA1.Contains("RunA", StringComparer.Ordinal), "Expected initial symbol extraction for AService.");

    var second = await indexer.IndexProject();
    AssertTrue(second.Success, "Expected second index pass success.");
    AssertTrue(indexer.CacheHits >= 2, "Expected cache hits when files are unchanged.");
    AssertTrue(indexer.CacheMisses == 0, "Expected no cache misses when all files are unchanged.");

    await Task.Delay(1100);
    await File.WriteAllTextAsync(fileA, "public class AService { public void RunA() {} public void RunA2() {} }");
    var third = await indexer.IndexProject();
    AssertTrue(third.Success, "Expected third index pass success after single-file change.");
    AssertTrue(indexer.CacheHits >= 1, "Expected at least one cache hit for unchanged file.");
    AssertTrue(indexer.CacheMisses == 1, "Expected exactly one cache miss for changed file.");
    var symbolsA2 = indexer.SymbolDirectory.GetSymbols(relA);
    var symbolsB2 = indexer.SymbolDirectory.GetSymbols(relB);
    AssertTrue(symbolsA2.Contains("RunA2", StringComparer.Ordinal), "Expected updated symbols after changed file reindex.");
    AssertTrue(symbolsB2.Contains("RunB", StringComparer.Ordinal), "Expected unchanged file symbols to remain available from cache.");

    Console.WriteLine("PASS MtimeAwareIndexingCache");
}

static void RunProjectMap_ClassifiesZonesAndRoles_Deterministically()
{
    var files = new[]
    {
        @"Core/Agent.cs",
        @"Context/ContextBuilder.cs",
        @"Indexing/ProjectIndexer.cs",
        @"Security/PermissionGuard.cs",
        @"Execution/BuildVerifier.cs",
        @"Tools/FileTool.cs",
        @"Diagnostics/ExecutionTracer.cs",
        @"Memory/MemoryStore.cs",
        @"LLM/OllamaClient.cs",
        @"SafetyTests/Program.cs",
        @"vscode-extension/webviewClient.js",
        @"scripts/devtools/Doctor.cmd",
        @"desktop-app/main.js",
        @"README.md",
        @"Configuration/appsettings.json"
    };

    var map1 = ProjectMapBuilder.Build("repo", files);
    var map2 = ProjectMapBuilder.Build("repo", files.Reverse());

    AssertTrue(map1.FileCount == files.Length, "Expected all files to be classified.");
    AssertTrue(map1.Files.Select(x => x.Path).SequenceEqual(map2.Files.Select(x => x.Path), StringComparer.OrdinalIgnoreCase), "Expected deterministic file ordering.");
    AssertTrue(map1.Files.Any(x => x.Zone == "Core"), "Expected Core zone.");
    AssertTrue(map1.Files.Any(x => x.Zone == "vscode-extension"), "Expected vscode-extension zone.");
    AssertTrue(map1.Files.Any(x => x.Role == "security"), "Expected security role.");
    AssertTrue(map1.Files.Any(x => x.Role == "devtool"), "Expected devtool role.");
    Console.WriteLine("PASS ProjectMap_ClassifiesZonesAndRoles_Deterministically");
}

static void RunProjectMap_EntrypointsDetected_ByNameRules()
{
    var files = new[]
    {
        "Program.cs",
        "LocalCursorAgent.csproj",
        "vscode-extension/extension.js",
        "vscode-extension/package.json",
        "desktop-app/main.js",
        "Core/Agent.cs"
    };
    var map = ProjectMapBuilder.Build("repo", files);
    AssertTrue(map.Files.First(x => x.Path.Equals("Program.cs", StringComparison.OrdinalIgnoreCase)).IsEntrypoint, "Program.cs should be entrypoint.");
    AssertTrue(map.Files.First(x => x.Path.Equals("LocalCursorAgent.csproj", StringComparison.OrdinalIgnoreCase)).IsEntrypoint, ".csproj should be entrypoint.");
    AssertTrue(map.Files.First(x => x.Path.Equals("vscode-extension/package.json", StringComparison.OrdinalIgnoreCase)).IsEntrypoint, "package.json should be entrypoint.");
    AssertTrue(!map.Files.First(x => x.Path.Equals("Core/Agent.cs", StringComparison.OrdinalIgnoreCase)).IsEntrypoint, "Core/Agent.cs should not be entrypoint.");
    Console.WriteLine("PASS ProjectMap_EntrypointsDetected_ByNameRules");
}

static void RunProjectMap_UnknownPaths_FallbackStable()
{
    var files = new[]
    {
        "weird/unknown.xyz",
        "misc/notes.txt"
    };
    var map = ProjectMapBuilder.Build("repo", files);
    AssertTrue(map.Files.All(x => x.Zone == "docs/config"), "Unknown zone should fallback to docs/config.");
    AssertTrue(map.Files.All(x => x.Role == "config"), "Unknown role should fallback to config.");
    Console.WriteLine("PASS ProjectMap_UnknownPaths_FallbackStable");
}

static void RunProjectRetrievalPlanner_ZoneRoleSelection()
{
    var snapshot = ProjectMapBuilder.Build("repo", new[]
    {
        "vscode-extension/webviewClientResultHandlers.js",
        "Security/PermissionGuard.cs",
        "Context/ContextBuilder.cs",
        "Indexing/ProjectIndexer.cs",
        "scripts/devtools/Doctor.cmd",
        "SafetyTests/Program.cs"
    });

    var uiPlan = ProjectRetrievalPlanner.Plan("fix webview panel status rendering in extension", snapshot);
    AssertTrue(uiPlan.SelectedZones.Contains("vscode-extension", StringComparer.OrdinalIgnoreCase), "UI task should target vscode-extension zone.");
    AssertTrue(uiPlan.SelectedRoles.Contains("extension-ui", StringComparer.OrdinalIgnoreCase), "UI task should target extension-ui role.");

    var safetyPlan = ProjectRetrievalPlanner.Plan("tighten permission guard and approval safety tests", snapshot);
    AssertTrue(safetyPlan.SelectedZones.Contains("Security", StringComparer.OrdinalIgnoreCase), "Safety task should target Security zone.");
    AssertTrue(safetyPlan.SelectedRoles.Contains("test", StringComparer.OrdinalIgnoreCase), "Safety task should include test role.");

    var doctorPlan = ProjectRetrievalPlanner.Plan("doctor smoke devtools script check", snapshot);
    AssertTrue(doctorPlan.SelectedZones.Contains("scripts/devtools", StringComparer.OrdinalIgnoreCase), "Doctor task should target devtools zone.");
    AssertTrue(doctorPlan.SelectedRoles.Contains("devtool", StringComparer.OrdinalIgnoreCase), "Doctor task should target devtool role.");

    Console.WriteLine("PASS ProjectRetrievalPlanner_ZoneRoleSelection");
}

static void RunProjectRetrievalPlanner_UnknownFallback()
{
    var snapshot = ProjectMapBuilder.Build("repo", new[] { "Core/Agent.cs" });
    var plan = ProjectRetrievalPlanner.Plan("blabla qwerty non-domain topic", snapshot);
    AssertTrue(plan.FallbackUsed, "Unknown task should fallback.");
    AssertTrue(plan.Confidence == 0.0, "Unknown task confidence should be 0.");
    Console.WriteLine("PASS ProjectRetrievalPlanner_UnknownFallback");
}

static void RunRetrievalSignalScorer_TargetedRanking()
{
    var snapshot = ProjectMapBuilder.Build("repo", new[]
    {
        "Security/PermissionGuard.cs",
        "Tools/FileTool.cs",
        "Execution/SafeProcessRunner.cs",
        "Security/CommandRiskPolicy.cs",
        "Context/ContextBuilder.cs",
        "Core/Agent.ContextPreparation.cs",
        "Core/AnalysisPromptBuilder.cs",
        "Core/AnalysisContextFormatter.cs",
        "Core/Agent.ToolingOrchestration.PrecheckHelpers.cs",
        "Core/Agent.RunResultPayloadBuilder.cs",
        "vscode-extension/workspaceResolver.js",
        "vscode-extension/workspaceTaskClassifier.js",
        "vscode-extension/panelRunController.js",
        "vscode-extension/commandHandlers.js",
        "scripts/devtools/Update-VSCodeExtension.cmd",
        "vscode-extension/package.json",
        "scripts/Create-SourceSnapshot.ps1",
        "scripts/devtools/Create-SourceSnapshot.cmd",
        ".editorconfig",
        ".gitattributes",
        "vscode-extension/encodingGuard.test.js",
        "scripts/devtools/encoding-precommit-check.js",
        "Docs/readme.md"
    });

    var approval = RetrievalSignalScorer.Score("Find approval token security issues in tooling", snapshot);
    AssertTrue(approval.Take(5).Any(x => x.Path.Equals("Tools/FileTool.cs", StringComparison.OrdinalIgnoreCase)), "Approval/security query should include FileTool.");
    AssertTrue(approval.Take(8).Any(x => x.Path.StartsWith("Core/Agent.Tooling", StringComparison.OrdinalIgnoreCase)), "Approval/security query should include Agent.Tooling files.");

    var process = RetrievalSignalScorer.Score("Audit command process shell handling", snapshot);
    var safeRunnerRank = process.FindIndex(x => x.Path.EndsWith("SafeProcessRunner.cs", StringComparison.OrdinalIgnoreCase));
    var diagRank = process.FindIndex(x => x.Path.Contains("ExecutionTracer", StringComparison.OrdinalIgnoreCase));
    AssertTrue(safeRunnerRank >= 0, "Process query should include SafeProcessRunner.");
    AssertTrue(diagRank < 0 || safeRunnerRank < diagRank, "SafeProcessRunner should outrank tracer diagnostics.");
    AssertTrue(process.Take(4).Any(x => x.Path.EndsWith("CommandRiskPolicy.cs", StringComparison.OrdinalIgnoreCase)), "Process query should rank CommandRiskPolicy.");
    AssertTrue(process.Take(8).Any(x => x.Path.StartsWith("Tools/", StringComparison.OrdinalIgnoreCase)), "Process query should include Tools files.");

    var workspace = RetrievalSignalScorer.Score("Что можно обойти в workspace guard и approval tokens", snapshot);
    AssertTrue(workspace.Take(8).Any(x => x.Path.EndsWith("workspaceResolver.js", StringComparison.OrdinalIgnoreCase)), "Workspace query should include workspaceResolver.");
    AssertTrue(workspace.Take(8).Any(x => x.Path.EndsWith("workspaceTaskClassifier.js", StringComparison.OrdinalIgnoreCase)), "Workspace query should include workspaceTaskClassifier.");
    AssertTrue(workspace.Take(8).Any(x => x.Path.EndsWith("panelRunController.js", StringComparison.OrdinalIgnoreCase)), "Workspace query should include panelRunController.");
    AssertTrue(workspace.Take(8).Any(x => x.Path.EndsWith("commandHandlers.js", StringComparison.OrdinalIgnoreCase)), "Workspace query should include commandHandlers.");

    var retrieval = RetrievalSignalScorer.Score("Improve retrieval context preparation", snapshot);
    AssertTrue(retrieval.Take(8).Any(x => x.Path.Equals("Context/ContextBuilder.cs", StringComparison.OrdinalIgnoreCase)), "Retrieval query should include ContextBuilder.cs.");
    AssertTrue(retrieval.Take(8).Any(x => x.Path.Equals("Core/Agent.ContextPreparation.cs", StringComparison.OrdinalIgnoreCase)), "Retrieval query should include Agent.ContextPreparation.cs.");
    AssertTrue(retrieval.Take(8).Any(x => x.Path.Equals("Core/AnalysisPromptBuilder.cs", StringComparison.OrdinalIgnoreCase)), "Retrieval query should include AnalysisPromptBuilder.cs.");

    var update = RetrievalSignalScorer.Score("Find stale VSIX/install workflow risks", snapshot);
    AssertTrue(update.Take(4).Any(x => x.Path.Equals("scripts/devtools/Update-VSCodeExtension.cmd", StringComparison.OrdinalIgnoreCase) || x.Path.Equals("vscode-extension/package.json", StringComparison.OrdinalIgnoreCase)), "Update query should rank update/package files.");

    var unrelated = RetrievalSignalScorer.Score("refactor readme wording", snapshot);
    AssertTrue(!(unrelated.FirstOrDefault()?.Path ?? string.Empty).StartsWith("Security/", StringComparison.OrdinalIgnoreCase), "Unrelated query should not over-boost security files to top rank.");
    Console.WriteLine("PASS RetrievalSignalScorer_TargetedRanking");
}

static void RunRetrievalSignalScorer_Determinism()
{
    var snapshot = ProjectMapBuilder.Build("repo", new[]
    {
        "Execution/SafeProcessRunner.cs",
        "Security/CommandRiskPolicy.cs",
        "Core/Agent.ContextPreparation.cs",
        "Context/ContextBuilder.cs"
    });
    var q = "Audit command process shell handling";
    var first = RetrievalSignalScorer.Score(q, snapshot).Select(x => $"{x.Path}:{x.Score:F4}:{string.Join(",", x.Reasons)}").ToArray();
    var second = RetrievalSignalScorer.Score(q, snapshot).Select(x => $"{x.Path}:{x.Score:F4}:{string.Join(",", x.Reasons)}").ToArray();
    AssertTrue(first.SequenceEqual(second), "Retrieval scoring should be deterministic for same input.");
    Console.WriteLine("PASS RetrievalSignalScorer_Determinism");
}

static async Task RunRetrievalDiagnostics_TopSignalsAndVsixDeepMode()
{
    var t = typeof(Agent).Assembly.GetType("LocalCursorAgent.Core.AnalysisPromptBuilder", throwOnError: true)!;
    var deepCheck = t.GetMethod("IsDeepAnalysisTask", new[] { typeof(string) })!;
    AssertTrue((bool)deepCheck.Invoke(null, new object[] { "Find stale VSIX/install workflow risks" })!, "Expected VSIX/install workflow query to be detected as deep analysis task.");

    var snapshot = ProjectMapBuilder.Build("repo", new[]
    {
        "scripts/devtools/Update-VSCodeExtension.cmd",
        "vscode-extension/package.json",
        "scripts/devtools/Doctor-Quick.cmd",
        "Core/Agent.cs"
    });
    var plan = ProjectRetrievalPlanner.Plan("Find stale VSIX/install workflow risks", snapshot);
    AssertTrue(plan.TopSignalFiles.Count <= 5, "Expected compact topSignalFiles.");
    AssertTrue(plan.TopSignalReasons.Count <= 5, "Expected compact topSignalReasons.");
    AssertTrue(plan.TopSignalFiles.Any(x => x.Equals("scripts/devtools/Update-VSCodeExtension.cmd", StringComparison.OrdinalIgnoreCase) || x.Equals("vscode-extension/package.json", StringComparison.OrdinalIgnoreCase)), "Expected VSIX/install top signal files.");
    var repeat = ProjectRetrievalPlanner.Plan("Find stale VSIX/install workflow risks", snapshot);
    AssertTrue(plan.TopSignalFiles.SequenceEqual(repeat.TopSignalFiles), "Expected deterministic topSignalFiles ordering.");
    await Task.CompletedTask;
    Console.WriteLine("PASS RetrievalDiagnostics_TopSignalsAndVsixDeepMode");
}

static async Task RunPlanningSummary_ContextAnalysisRegression()
{
    var structured = await RunIntentMatrixTask("сделай code review context indexing retrieval, ничего не меняй", new FakeNoToolAnalysisClient(), registerFileTool: true);
    var reason = structured.GetProperty("reasonCode").GetString() ?? string.Empty;
    AssertTrue(!string.Equals(reason, "SUCCESS_NO_TOOL_CALLS", StringComparison.OrdinalIgnoreCase), "Expected non-chat route for analysis request.");
    var planningSummary = structured.GetProperty("planningSummary").GetString() ?? string.Empty;
    AssertTrue(!string.IsNullOrWhiteSpace(planningSummary), "Expected non-empty planningSummary payload for context analysis.");
    AssertTrue(planningSummary.Contains("Context", StringComparison.OrdinalIgnoreCase), "Expected Context zone in planning summary.");
    Console.WriteLine("PASS PlanningSummary_ContextAnalysis");
}

static async Task RunPlanningSummary_UiAnalysisRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    Directory.CreateDirectory(Path.Combine(workspaceRoot, "vscode-extension"));
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "vscode-extension", "webviewClient.js"), "export const x = 1;");
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "Program.cs"), "public static class Program { public static void Main(){} }");

    var tracer = new ExecutionTracer(runtimeRoot);
    tracer.StartRun("сделай code review extension webview panel status, без правок", "сделай code review extension webview panel status, без правок", workspaceRoot, runtimeRoot, AgentAccessMode.WorkspaceWrite.ToString(), "FakeNoToolAnalysisClient", "fake-ui-analysis-model");
    var session = new AgentSessionContext
    {
        SessionId = Guid.NewGuid().ToString("N"),
        RuntimeRoot = runtimeRoot,
        ActiveWorkspaceRoot = workspaceRoot,
        AccessMode = AgentAccessMode.WorkspaceWrite,
        ProtectedPathPolicy = new ProtectedPathPolicy(new[] { runtimeRoot })
    };
    var agent = CreateSimpleAgentForIntentTests(workspaceRoot, runtimeRoot, session, tracer, new FakeNoToolAnalysisClient());
    var oldOut = Console.Out;
    var capture = new StringWriter();
    Console.SetOut(capture);
    try { _ = await agent.RunTask("сделай code review extension webview panel status, без правок"); } finally { Console.SetOut(oldOut); }
    var structured = ExtractStructuredPayload(capture.ToString());

    var planningSummary = structured.GetProperty("planningSummary").GetString() ?? string.Empty;
    AssertTrue(planningSummary.Contains("vscode-extension", StringComparison.OrdinalIgnoreCase), "Expected planningSummary payload to include vscode-extension.");
    Console.WriteLine("PASS PlanningSummary_UiAnalysis");
}

static async Task RunPlanningSummary_UnknownFallbackRegression()
{
    var structured = await RunIntentMatrixTask("проанализируй qwerty zyxwvu без правок", new FakeNoToolAnalysisClient(), registerFileTool: true);
    var planningSummary = structured.GetProperty("planningSummary").GetString() ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(planningSummary))
    {
        AssertTrue(planningSummary.Contains("обычный context selection", StringComparison.OrdinalIgnoreCase), "Expected fallback planning summary text.");
    }
    Console.WriteLine("PASS PlanningSummary_UnknownFallback");
}

static async Task RunPlanningSummary_ChatNoNoiseRegression()
{
    var structured = await RunIntentMatrixTask("привет", new FakeNoToolAnalysisClient(), registerFileTool: true);
    var planningSummary = structured.GetProperty("planningSummary").GetString() ?? string.Empty;
    AssertTrue(string.IsNullOrWhiteSpace(planningSummary), "Expected no planning summary for simple chat.");
    var message = structured.GetProperty("message").GetString() ?? string.Empty;
    AssertTrue(!message.StartsWith("План:", StringComparison.OrdinalIgnoreCase), "Expected no planning prefix for chat message.");
    Console.WriteLine("PASS PlanningSummary_ChatNoNoise");
}

static async Task RunTaskPlan_AnalysisMode_NoMutationChecks()
{
    var structured = await RunIntentMatrixTask("сделай code review context indexing retrieval, ничего не меняй", new FakeNoToolAnalysisClient(), registerFileTool: true);
    AssertTrue(structured.TryGetProperty("taskPlan", out var taskPlan) && taskPlan.ValueKind == JsonValueKind.Object, "Expected taskPlan for analysis.");
    AssertTrue(string.Equals(taskPlan.GetProperty("mode").GetString(), "analysis", StringComparison.OrdinalIgnoreCase), "Expected analysis mode.");
    AssertTrue(taskPlan.GetProperty("steps").EnumerateArray().Any(x => string.Equals(x.GetString(), "inspect", StringComparison.OrdinalIgnoreCase)), "Expected inspect step.");
    AssertTrue(taskPlan.GetProperty("checks").EnumerateArray().Any(x => string.Equals(x.GetString(), "no_file_changes", StringComparison.OrdinalIgnoreCase)), "Expected no_file_changes check.");
    Console.WriteLine("PASS TaskPlan_AnalysisMode_NoMutationChecks");
}

static async Task RunTaskPlan_ExecuteMode_InspectEditTestFlow()
{
    var structured = await RunIntentMatrixTask("исправь ошибку в ContextBuilder.cs", new FakeNoToolAnalysisClient(), registerFileTool: true);
    AssertTrue(structured.TryGetProperty("taskPlan", out var taskPlan) && taskPlan.ValueKind == JsonValueKind.Object, "Expected taskPlan for execute.");
    AssertTrue(string.Equals(taskPlan.GetProperty("mode").GetString(), "execute", StringComparison.OrdinalIgnoreCase), "Expected execute mode.");
    var steps = taskPlan.GetProperty("steps").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
    AssertContains(steps, "inspect");
    AssertContains(steps, "edit");
    AssertContains(steps, "test");
    Console.WriteLine("PASS TaskPlan_ExecuteMode_InspectEditTestFlow");
}

static async Task RunTaskPlan_ChatMode_NotEmitted()
{
    var structured = await RunIntentMatrixTask("привет", new FakeNoToolAnalysisClient(), registerFileTool: true);
    AssertTrue(!structured.TryGetProperty("taskPlan", out var taskPlan) || taskPlan.ValueKind == JsonValueKind.Null, "Expected no taskPlan for chat.");
    Console.WriteLine("PASS TaskPlan_ChatMode_NotEmitted");
}

static async Task RunTaskPlan_ClarifyMode_NotEmitted()
{
    var structured = await RunIntentMatrixTask("сделай нормально", new FakeNoToolAnalysisClient(), registerFileTool: true);
    AssertTrue(!structured.TryGetProperty("taskPlan", out var taskPlan) || taskPlan.ValueKind == JsonValueKind.Null, "Expected no taskPlan for clarify.");
    Console.WriteLine("PASS TaskPlan_ClarifyMode_NotEmitted");
}

static async Task RunTaskPlan_CandidateFiles_MaxFive()
{
    var structured = await RunIntentMatrixTask("исправь ошибку в ContextBuilder.cs и соседних файлах", new FakeNoToolAnalysisClient(), registerFileTool: true);
    if (structured.TryGetProperty("taskPlan", out var taskPlan) && taskPlan.ValueKind == JsonValueKind.Object)
    {
        var files = taskPlan.GetProperty("candidateFiles");
        AssertTrue(files.ValueKind == JsonValueKind.Array && files.GetArrayLength() <= 5, "Expected candidateFiles max 5.");
    }
    Console.WriteLine("PASS TaskPlan_CandidateFiles_MaxFive");
}

static async Task RunTaskPlan_StopConditions_UnsafePathApprovalAmbiguous()
{
    var structured = await RunIntentMatrixTask("исправь баг и если надо трогай защищенные пути", new FakeNoToolAnalysisClient(), registerFileTool: true);
    if (structured.TryGetProperty("taskPlan", out var taskPlan) && taskPlan.ValueKind == JsonValueKind.Object)
    {
        var stop = taskPlan.GetProperty("stopConditions").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
        AssertContains(stop, "unsafe_path");
        AssertContains(stop, "approval_required");
        AssertContains(stop, "ambiguous_scope");
    }
    Console.WriteLine("PASS TaskPlan_StopConditions_UnsafePathApprovalAmbiguous");
}

static async Task RunTaskPlan_Checks_ByZone()
{
    var ui = await RunIntentMatrixTask("исправь webview status", new FakeNoToolAnalysisClient(), registerFileTool: true);
    if (ui.TryGetProperty("taskPlan", out var uiPlan) && uiPlan.ValueKind == JsonValueKind.Object)
    {
        var checks = uiPlan.GetProperty("checks").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
        AssertContains(checks, "npm test");
    }

    var core = await RunIntentMatrixTask("исправь ошибку в ContextBuilder.cs", new FakeNoToolAnalysisClient(), registerFileTool: true);
    if (core.TryGetProperty("taskPlan", out var corePlan) && corePlan.ValueKind == JsonValueKind.Object)
    {
        var checks = corePlan.GetProperty("checks").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
        AssertContains(checks, "SmokeGate");
        AssertContains(checks, "SafetyTests");
    }

    var dev = await RunIntentMatrixTask("обнови doctor script", new FakeNoToolAnalysisClient(), registerFileTool: true);
    if (dev.TryGetProperty("taskPlan", out var devPlan) && devPlan.ValueKind == JsonValueKind.Object)
    {
        var checks = devPlan.GetProperty("checks").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
        AssertContains(checks, "Doctor-Quick");
        AssertContains(checks, "SmokeGate");
    }

    Console.WriteLine("PASS TaskPlan_Checks_ByZone");
}

static async Task RunContextSelection_UsesProjectMapHints_WithoutBreakingBudget()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalCursorAgentSafetyTests", Guid.NewGuid().ToString("N"));
    var workspaceRoot = Path.Combine(tempRoot, "workspace");
    var runtimeRoot = Path.Combine(tempRoot, "runtime");
    Directory.CreateDirectory(workspaceRoot);
    Directory.CreateDirectory(runtimeRoot);
    Directory.CreateDirectory(Path.Combine(workspaceRoot, "Core"));
    Directory.CreateDirectory(Path.Combine(workspaceRoot, "SafetyTests"));

    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "Core", "Agent.cs"), new string('a', 10000));
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "SafetyTests", "AgentFlow.test.cs"), new string('b', 9000));
    await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "Core", "Noise.cs"), new string('c', 14000));

    var tracer = new ExecutionTracer(runtimeRoot);
    var builder = new ContextBuilder(workspaceRoot, new VectorStore(), new FileStateManager(), new ProjectSymbolDirectory(), tracer);
    var semantic = new List<string> { "Core/Agent.cs", "Core/Noise.cs", "SafetyTests/AgentFlow.test.cs" };
    var symbols = new List<string> { "Core/Agent.cs" };
    var map = ProjectMapBuilder.Build(workspaceRoot, semantic);

    var info = builder.BuildContext("add regression test for agent", semantic, symbols, 6);
    AssertTrue(info.TotalLength <= 30000, "Budget should remain bounded.");
    AssertTrue(info.SelectedFiles.Count > 0, "Expected non-empty context.");
    AssertTrue(map.Files.Any(x => x.Path.Equals("SafetyTests/AgentFlow.test.cs", StringComparison.OrdinalIgnoreCase) && x.Role == "test"), "Expected ProjectMap test role classification.");
    Console.WriteLine("PASS ContextSelection_UsesProjectMapHints_WithoutBreakingBudget");
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

sealed class FakeMutationToolCallClient : ILLMClient
{
    public Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult("TOOL:file\nINPUT:write:Calculator.cs:public class Calculator { }");

    public Task<bool> IsAvailable(CancellationToken cancellationToken = default)
        => Task.FromResult(true);
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

