const vscode = require('vscode');

function createPanelRunController(options) {
  const panel = options.panel;
  const output = options.output;
  const extensionRoot = options.extensionRoot;
  const resolveWorkspaceRoot = options.resolveWorkspaceRoot;
  const runAgent = options.runAgent;
  const getIsAgentRunning = options.getIsAgentRunning;
  const setIsAgentRunning = options.setIsAgentRunning;

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
      const workspaceState = resolveWorkspaceRoot();
      if (!workspaceState.workspaceRoot && workspaceState.reason === 'not_found') {
        postWorkspaceFailure('Workspace not found');
        return;
      }

      if (!workspaceState.workspaceRoot) {
        postWorkspaceFailure('Cannot determine active workspace');
        return;
      }

      panel.webview.postMessage({ type: 'runningState', running: true });
      setIsAgentRunning(true);
      vscode.window.setStatusBarMessage('Agent running...', 0);
      output.appendLine('Agent run started');

      const selectedModel = String(message.selectedModel || message.model || '').trim();
      const result = await runAgent(panel, workspaceState.workspaceRoot, task, output, extensionRoot, selectedModel);
      const resultText = (result.result && result.result.message) || result.text;
      panel.webview.postMessage({ type: 'result', text: resultText });
      panel.webview.postMessage({
        type: 'agentFinished',
        ok: true,
        result: resultText || 'Agent run completed successfully.',
        structuredResult: result.result || null
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

  function postWorkspaceFailure(text) {
    panel.webview.postMessage({ type: 'result', text });
    panel.webview.postMessage({ type: 'agentFinished', ok: false, error: text });
  }

  return { handleSendTask };
}

module.exports = { createPanelRunController };
