using LocalCursorAgent.Execution;
using System.Linq;

namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private bool TryRepairCs8802(BuildVerifier.BuildResult buildResult, HashSet<string> changedFiles, out string? nextPrompt)
        {
            nextPrompt = null;
            var cs8802 = buildResult.Errors.FirstOrDefault(e => e.Contains("CS8802", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(cs8802) || _sessionContext == null)
            {
                return false;
            }

            var programPath = Path.Combine(_sessionContext.ActiveWorkspaceRoot, "Program.cs");
            if (!File.Exists(programPath) || !TopLevelStatementInspector.ContainsTopLevelStatements(programPath))
            {
                return false;
            }

            foreach (var changedFile in changedFiles.Where(f =>
                         f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                         !Path.GetFileName(f).Equals("Program.cs", StringComparison.OrdinalIgnoreCase)))
            {
                if (!File.Exists(changedFile))
                {
                    continue;
                }

                var fileText = File.ReadAllText(changedFile);
                if (!TopLevelStatementInspector.ContainsMainEntryPoint(fileText))
                {
                    continue;
                }

                var normalized = TopLevelStatementInspector.NormalizeHelperClassWithoutMain(fileText);
                if (string.Equals(normalized, fileText, StringComparison.Ordinal))
                {
                    continue;
                }

                File.WriteAllText(changedFile, normalized);
                _fileStateManager.MarkHot(changedFile);
                nextPrompt = $"Detected CS8802 with top-level Program.cs. Removed Main entry point from {Path.GetFileName(changedFile)} and normalized it into helper/class form. Re-run build and continue fixing only remaining errors.";
                return true;
            }

            nextPrompt = "Detected CS8802 with top-level Program.cs. Do not rewrite Program.cs by default. Inspect newly created .cs files and remove any extra Main entry point or top-level executable code from them.";
            return true;
        }

        private string BuildPromptWithContext(string task, int iteration, string previousResponse, string codeContext, string regressionAdvice, string promptShapingAdvice, string strategyBiasAdvice)
        {
            var executionContext = _memory.GetContextString(CONTEXT_WINDOW);
            var taskProfile = _memorySystem.GetTaskProfileSummary(task);
            var toolsDescription = _toolRegistry.GetToolsDescription();
            var policyBlock = WorkspacePolicyFormatter.BuildPolicyBlock(_sessionContext);
            var startupStateBlock = StartupStateFormatter.BuildStartupStateBlock(_sessionContext, _workspaceResolution);
            return ContextPromptBuilder.BuildPromptWithContext(
                task,
                iteration,
                previousResponse,
                codeContext,
                regressionAdvice,
                promptShapingAdvice,
                strategyBiasAdvice,
                executionContext,
                taskProfile,
                toolsDescription,
                policyBlock,
                startupStateBlock,
                ResponseLanguageHelper.BuildResponseLanguageRule(task));
        }
    }
}
