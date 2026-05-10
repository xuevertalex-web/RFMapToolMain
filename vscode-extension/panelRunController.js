const vscode = require('vscode');
function isAnalysisOnlyTask(task) {
  const value = String(task || '').toLowerCase();
  return value.includes('analyze') ||
    value.includes('analyse') ||
    value.includes('summarize') ||
    value.includes('summarise') ||
    value.includes('explain') ||
    value.includes('review') ||
    value.includes('diagnose') ||
    value.includes('опиши') ||
    value.includes('описать') ||
    value.includes('объясни') ||
    value.includes('объяснить') ||
    value.includes('обзор') ||
    value.includes('структуру') ||
    value.includes('структура') ||
    value.includes('ключевые файлы') ||
    value.includes('расскажи');
}

function createPanelRunController(options) {
  const panel = options.panel;
  const output = options.output;
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

  async function handleSendTask(message) {
    const task = String(message.task || '').trim();
    if (!task) {
      panel.webview.postMessage({ type: 'result', text: 'Task is empty' });
      return;
    }

    if (getIsAgentRunning()) {
      vscode.window.showWarningMessage('Agent is already running');
      return;
    }

    try {
      const workspaceState = resolveWorkspaceRoot({
        initializeIfMissing: true,
        projectNameHint: task,
        analysisOnlyTask: isAnalysisOnlyTask(task)
      });
      if (!workspaceState.workspaceRoot) {
        postWorkspaceFailure(workspaceErrorText(workspaceState), workspaceState);
        return;
      }

      panel.webview.postMessage({ type: 'runningState', running: true });
      setIsAgentRunning(true);
      vscode.window.setStatusBarMessage('Agent running...', 0);
      output.appendLine('Agent run started');

      const selectedModel = String(message.selectedModel || message.model || '').trim();
      const result = await runAgent(
        panel,
        workspaceState.workspaceRoot,
        task,
        output,
        extensionRoot,
        selectedModel,
        message && typeof message.sessionContext === 'object' ? message.sessionContext : null
      );
      const resultText = (result.result && result.result.message) || result.text;
      const structuredResult = {
        ...(result.result || {}),
        workspaceInitializationRequired: false,
        workspaceInitialized: workspaceState.workspaceInitialized === true,
        workspaceInitializationMode: String(workspaceState.workspaceInitializationMode || 'existing'),
        targetWorkspacePath: String(workspaceState.targetWorkspacePath || ''),
        initializedProjectRoot: String(workspaceState.initializedProjectRoot || workspaceState.workspaceRoot || ''),
        suggestedProjectFolderName: String(workspaceState.suggestedProjectFolderName || ''),
        projectTemplateApplied: workspaceState.projectTemplateApplied === true,
        templateType: String(workspaceState.templateType || 'none')
      };
      panel.webview.postMessage({ type: 'result', text: resultText });
      panel.webview.postMessage({
        type: 'agentFinished',
        ok: true,
        result: resultText || 'Agent run completed successfully.',
        structuredResult
      });
      vscode.window.setStatusBarMessage('Agent finished', 3000);
    } catch (err) {
      const text = err instanceof Error ? err.message : String(err);
      const structuredResult = err && typeof err === 'object' ? err.result : null;
      if (text !== 'Agent stopped by user.') {
        const resultText = (structuredResult && structuredResult.message) || `Failed to run agent: ${text}`;
        panel.webview.postMessage({ type: 'result', text: resultText });
        panel.webview.postMessage({
          type: 'agentFinished',
          ok: false,
          error: text,
          result: resultText || undefined,
          structuredResult: structuredResult || null
        });
      }
      output.appendLine(text);
    } finally {
      panel.webview.postMessage({ type: 'runningState', running: false });
      setIsAgentRunning(false);
      output.appendLine('Agent run finished');
    }
  }

  function postWorkspaceFailure(text, workspaceState) {
    panel.webview.postMessage({ type: 'result', text });
    panel.webview.postMessage({
      type: 'agentFinished',
      ok: false,
      error: text,
      structuredResult: {
        ok: false,
        finalStatus: 'error',
        message: text,
        workspaceInitializationRequired: true,
        workspaceInitialized: false,
        workspaceInitializationMode: 'requires_user_selection',
        targetWorkspacePath: String(workspaceState && workspaceState.targetWorkspacePath || ''),
        initializedProjectRoot: String(workspaceState && workspaceState.initializedProjectRoot || ''),
        suggestedProjectFolderName: String(workspaceState && workspaceState.suggestedProjectFolderName || 'NewProject'),
        projectTemplateApplied: false,
        templateType: 'none'
      }
    });
  }

  return { handleSendTask };
}

module.exports = { createPanelRunController };
