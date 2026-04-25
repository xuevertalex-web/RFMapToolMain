const vscode = require('vscode');

function createExtensionCommandHandlers(options) {
  const output = options.output;
  const extensionRoot = options.extensionRoot;
  const resolveWorkspaceRoot = options.resolveWorkspaceRoot;
  const runAgent = options.runAgent;
  const getIsAgentRunning = options.getIsAgentRunning;
  const setIsAgentRunning = options.setIsAgentRunning;

  return {
    async handleRunTask() {
      const workspaceState = resolveWorkspaceRoot();
      if (!workspaceState.workspaceRoot && workspaceState.reason === 'not_found') {
        vscode.window.showErrorMessage('Workspace not found');
        return;
      }

      if (!workspaceState.workspaceRoot) {
        vscode.window.showErrorMessage('Cannot determine active workspace');
        return;
      }

      const task = await vscode.window.showInputBox({
        prompt: 'Describe the task for the local agent',
        placeHolder: 'Add validation to AuthService'
      });

      if (!task || !task.trim()) {
        vscode.window.showErrorMessage('Task is empty');
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
        const result = await runAgent(null, workspaceState.workspaceRoot, task.trim(), output, extensionRoot);
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
