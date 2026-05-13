const { createPanelRunController } = require('./panelRunController');
const { createPanelCommandHandlers } = require('./panelCommandHandlers');

function createPanelMessageHandler(options) {
  const panel = options.panel;
  const output = options.output;
  const runtimeLogger = options.runtimeLogger;
  const extensionRoot = options.extensionRoot;
  const resolveWorkspaceRoot = options.resolveWorkspaceRoot;
  const runAgent = options.runAgent;
  const hasRunningProcess = options.hasRunningProcess;
  const stopCurrentAgent = options.stopCurrentAgent;
  const editorNavigation = options.editorNavigation;
  const exportService = options.exportService;
  const panelRunController = createPanelRunController({
    panel,
    output,
    runtimeLogger,
    extensionRoot,
    resolveWorkspaceRoot,
    runAgent,
    getIsAgentRunning: options.getIsAgentRunning,
    setIsAgentRunning: options.setIsAgentRunning
  });
  const panelCommandHandlers = createPanelCommandHandlers({
    output,
    hasRunningProcess,
    stopCurrentAgent,
    editorNavigation
  });

  return async function handlePanelMessage(message) {
    const type = String(message && message.type || '');

    if (type === 'getModelSelectionState') {
      if (typeof options.getModelSelectionState === 'function') {
        const payload = await options.getModelSelectionState();
        panel.webview.postMessage({ type: 'modelSelectionState', payload });
      }
      return;
    }

    if (type === 'setSelectedModel') {
      if (typeof options.setSelectedModel === 'function') {
        const payload = await options.setSelectedModel(message.model);
        panel.webview.postMessage({ type: 'modelSelectionState', payload });
      }
      return;
    }

    if (type === 'sendTask') {
      await panelRunController.handleSendTask(message);
      return;
    }

    if (type === 'openFile') {
      await panelCommandHandlers.handleOpenFile(message);
      return;
    }

    if (type === 'openAllChangedFiles') {
      await editorNavigation.openAllChangedFiles(message.files);
      return;
    }

    if (type === 'exportRunReport') {
      await exportService.exportRunReport(message.payload);
      return;
    }

    if (type === 'exportLogs') {
      await exportService.exportLogs(message.text);
      return;
    }

    if (type === 'exportChangedFiles') {
      await exportService.exportChangedFiles(message.text);
      return;
    }

    if (type === 'copyToClipboard') {
      const text = String(message.text || '');
      const requestId = String(message.requestId || '');
      if (!text) {
        panel.webview.postMessage({
          type: 'copyToClipboardResult',
          requestId,
          ok: false,
          error: 'empty_copy_text'
        });
        return;
      }
      try {
        await require('vscode').env.clipboard.writeText(text);
        panel.webview.postMessage({
          type: 'copyToClipboardResult',
          requestId,
          ok: true
        });
      } catch (error) {
        panel.webview.postMessage({
          type: 'copyToClipboardResult',
          requestId,
          ok: false,
          error: String(error && error.message || error || 'clipboard_write_failed')
        });
      }
      return;
    }

    if (type === 'stopAgent') {
      panelCommandHandlers.handleStopAgent();
    }
  };
}

module.exports = { createPanelMessageHandler };
