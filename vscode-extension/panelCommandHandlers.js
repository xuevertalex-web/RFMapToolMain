const vscode = require('vscode');

function createPanelCommandHandlers(options) {
  const output = options.output;
  const hasRunningProcess = options.hasRunningProcess;
  const stopCurrentAgent = options.stopCurrentAgent;
  const editorNavigation = options.editorNavigation;

  return {
    async handleOpenFile(message) {
      const filePath = String(message.filePath || message.path || '').trim();
      if (!filePath) {
        return;
      }

      const requestedStartLine = Number.isFinite(Number(message.startLine)) ? Number(message.startLine) : null;
      const requestedEndLine = Number.isFinite(Number(message.endLine)) ? Number(message.endLine) : null;

      try {
        await editorNavigation.openFile(filePath, requestedStartLine, requestedEndLine, message.silent === true);
      } catch (err) {
        const text = err instanceof Error ? err.message : String(err);
        vscode.window.showErrorMessage(text);
        output.appendLine(text);
      }
    },

    handleStopAgent() {
      if (!hasRunningProcess()) {
        vscode.window.showWarningMessage('Agent is not running');
        return;
      }

      try {
        stopCurrentAgent(output);
        vscode.window.setStatusBarMessage('Agent stopped', 3000);
      } catch (err) {
        const text = err instanceof Error ? err.message : String(err);
        output.appendLine(text);
        vscode.window.showErrorMessage(text);
      }
    }
  };
}

module.exports = { createPanelCommandHandlers };
