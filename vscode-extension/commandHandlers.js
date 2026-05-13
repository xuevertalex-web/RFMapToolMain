const { isAnalysisOnlyTask } = require('./workspaceTaskClassifier');
const vscode = require('vscode');

const MUTATION_RE = /\b(create|delete|remove|rename|fix|edit|update|modify|change|\u0441\u043e\u0437\u0434\u0430\u0439|\u0443\u0434\u0430\u043b\u0438|\u043f\u0435\u0440\u0435\u0438\u043c\u0435\u043d\u0443\u0439|\u0438\u0441\u043f\u0440\u0430\u0432\u044c|\u043e\u0431\u043d\u043e\u0432\u0438|\u0438\u0437\u043c\u0435\u043d\u0438|\u043f\u043e\u043c\u0435\u043d\u044f\u0439|\u043e\u0442\u0440\u0435\u0434\u0430\u043a\u0442\u0438\u0440\u0443\u0439)\b/;
const LOW_SIGNAL_RE = /^(\u0442\u0443\u0442\??|\u0430\u043b\u043e|\u0430\u043b\u043b\u043e|here\??|what can you do|explain this project|\u043e\u043f\u0438\u0448\u0438 \u043f\u0440\u043e\u0435\u043a\u0442\??|\u0447\u0442\u043e \u0442\u0443\u0442\??|debug|\u0432\u0432\u0443\u0438\u0433\u043f)$/;

function isLowSignalChatFallback(task) {
  const value = String(task || '').trim().toLowerCase();
  if (!value) return true;
  if (MUTATION_RE.test(value)) {
    return false;
  }
  if (LOW_SIGNAL_RE.test(value)) {
    return true;
  }
  const compact = value.replace(/[\s?!.,;:]+/g, '');
  return compact.length > 0 && compact.length <= 8;
}

function buildLowSignalReply(task) {
  const value = String(task || '').trim().toLowerCase();
  const normalized = value.replace(/[?!.,;:]+$/g, '').trim();
  if (normalized.startsWith('\u043e\u043f\u0438\u0448\u0438 \u043f\u0440\u043e\u0435\u043a\u0442') || normalized.startsWith('explain this project')) {
    return '\u041c\u043e\u0433\u0443 \u043a\u0440\u0430\u0442\u043a\u043e \u043e\u043f\u0438\u0441\u0430\u0442\u044c \u043f\u0440\u043e\u0435\u043a\u0442. \u0423\u043a\u0430\u0436\u0438 \u043f\u0430\u043f\u043a\u0443/\u0440\u0435\u043f\u043e\u0437\u0438\u0442\u043e\u0440\u0438\u0439 \u0438\u043b\u0438 \u043e\u0442\u043a\u0440\u043e\u0439 \u043a\u043e\u0440\u0435\u043d\u044c workspace.';
  }
  return '\u042f \u043d\u0430 \u0441\u0432\u044f\u0437\u0438. \u0421\u0444\u043e\u0440\u043c\u0443\u043b\u0438\u0440\u0443\u0439 \u0437\u0430\u0434\u0430\u0447\u0443: \u0447\u0442\u043e \u0441\u043e\u0437\u0434\u0430\u0442\u044c, \u0438\u0437\u043c\u0435\u043d\u0438\u0442\u044c \u0438\u043b\u0438 \u043f\u0440\u043e\u0432\u0435\u0440\u0438\u0442\u044c.';
}
function createExtensionCommandHandlers(options) {
  const output = options.output;
  const runtimeLogger = options.runtimeLogger;
  const extensionRoot = options.extensionRoot;
  const resolveWorkspaceRoot = options.resolveWorkspaceRoot;
  const runAgent = options.runAgent;
  const getIsAgentRunning = options.getIsAgentRunning;
  const setIsAgentRunning = options.setIsAgentRunning;
  function workspaceErrorText(workspaceState) {
    const reason = String(workspaceState && workspaceState.reason || '');
    if (reason === 'configured_not_found') {
      return 'Configured target workspace path not found';
    }
    if (reason === 'backend_workspace_blocked') {
      return 'Backend workspace is blocked for execute/mutation tasks. Options: 1) Run as analysis-only, 2) Allow backend workspace for this run, 3) Open/choose target workspace.';
    }
    if (reason === 'not_found') {
      return 'Workspace not found. Set localCursorAgent.targetWorkspacePath in settings for empty VS Code windows.';
    }
    return 'Cannot determine active workspace';
  }

  return {
    async handleRunTask() {
      const task = await vscode.window.showInputBox({
        prompt: 'Describe the task for the local agent',
        placeHolder: 'Add validation to AuthService'
      });

      if (!task || !task.trim()) {
        vscode.window.showErrorMessage('Task is empty');
        return;
      }
      const trimmedTask = task.trim();
      const classifierAnalysisOnly = isAnalysisOnlyTask(trimmedTask);
      const fallbackAnalysisOnly = isLowSignalChatFallback(trimmedTask);
      if (fallbackAnalysisOnly) {
        const reply = buildLowSignalReply(trimmedTask);
        output.show(true);
        output.appendLine(`[workspace-guard] low-signal short-circuit source=command task="${trimmedTask}"`);
        output.appendLine(reply);
        if (runtimeLogger) runtimeLogger.info('workspace-guard low-signal short-circuit', { source: 'command', task: trimmedTask });
        vscode.window.showInformationMessage(reply);
        return;
      }
      const analysisOnlyTask = classifierAnalysisOnly || fallbackAnalysisOnly;
      output.appendLine(`[workspace-guard] precheck source=command task="${trimmedTask}" classifierAnalysisOnly=${classifierAnalysisOnly} fallbackAnalysisOnly=${fallbackAnalysisOnly} analysisOnlyTask=${analysisOnlyTask}`);
      if (runtimeLogger) runtimeLogger.info('workspace-guard precheck', { source: 'command', task: trimmedTask, classifierAnalysisOnly, fallbackAnalysisOnly, analysisOnlyTask });
      const workspaceState = resolveWorkspaceRoot({
        initializeIfMissing: true,
        analysisOnlyTask,
        taskText: trimmedTask
      });
      if (!workspaceState.workspaceRoot) {
        output.appendLine(`[workspace-guard] blocked source=command reason=${String(workspaceState.reason || 'unknown')} targetWorkspacePath=${String(workspaceState.targetWorkspacePath || '')}`);
        if (runtimeLogger) runtimeLogger.warn('workspace-guard blocked', { source: 'command', reason: String(workspaceState.reason || 'unknown'), targetWorkspacePath: String(workspaceState.targetWorkspacePath || '') });
        vscode.window.showErrorMessage(workspaceErrorText(workspaceState));
        return;
      }

      if (getIsAgentRunning()) {
        vscode.window.showWarningMessage('Agent is already running');
        return;
      }

      output.show(true);
      setIsAgentRunning(true);
      vscode.window.setStatusBarMessage('Agent running...', 0);
      output.appendLine('Agent run started');

      try {
        const result = await runAgent(null, workspaceState.workspaceRoot, trimmedTask, output, extensionRoot);
        const resultText = (result.result && result.result.message) || result.text;
        output.appendLine('--- Agent result start ---');
        output.appendLine(resultText || 'No result text returned.');
        output.appendLine('--- Agent result end ---');
        if (resultText && resultText.length <= 250) {
          vscode.window.showInformationMessage(resultText);
        } else {
          vscode.window.showInformationMessage('Agent finished. Full response is in Output: Local Cursor Agent');
        }
        vscode.window.setStatusBarMessage('Agent finished', 3000);
      } catch (err) {
        const text = err instanceof Error ? err.message : String(err);
        const structuredResult = err && typeof err === 'object' ? err.result : null;
        if (text !== 'Agent stopped by user.') {
          output.show(true);
          vscode.window.showErrorMessage((structuredResult && structuredResult.message) || text);
          output.appendLine(text);
        }
        vscode.window.setStatusBarMessage('Agent finished', 3000);
      } finally {
        setIsAgentRunning(false);
        output.appendLine('Agent run finished');
      }
    }
  };
}

module.exports = { createExtensionCommandHandlers };
