const vscode = require('vscode');
const { runAgent, hasRunningProcess, stopCurrentAgent } = require('./agentRunner');
const { resolveWorkspaceRoot } = require('./workspaceResolver');
const { getHtml } = require('./webviewHtml');
const { createEditorNavigationService } = require('./editorNavigation');
const { createExportService } = require('./exportService');
const { createPanelMessageHandler } = require('./messageRouter');
const { createExtensionCommandHandlers } = require('./commandHandlers');
const {
  loadSelectedOllamaModel,
  getSelectedOllamaModelSync,
  saveSelectedOllamaModel,
  buildModelSelectionStateWithPing
} = require('./modelSelection');

let isAgentRunning = false;

function activate(context) {
  const output = vscode.window.createOutputChannel('Local Cursor Agent');
  try {
    output.appendLine('Local Cursor Agent activation started');

    const extensionRoot = context.extensionPath;
    const changedRangeDecoration = vscode.window.createTextEditorDecorationType({
      backgroundColor: 'rgba(9, 105, 218, 0.18)',
      border: '1px solid rgba(9, 105, 218, 0.45)',
      borderRadius: '3px'
    });

    const editorNavigation = createEditorNavigationService({
      changedRangeDecoration,
      output,
      resolveWorkspaceRoot
    });
    const exportService = createExportService({
      extensionRoot,
      output,
      resolveWorkspaceRoot
    });
    const getBackendProjectPath = () => String(
      vscode.workspace.getConfiguration('localCursorAgent').get('backendProjectPath') || ''
    ).trim();
    let selectedModelState = getSelectedOllamaModelSync(context.globalState);
    const runConfiguredAgent = (panel, workspaceRoot, task, runOutput, runExtensionRoot, selectedModelOverride) => {
      const runModel = String(selectedModelOverride || '').trim() || selectedModelState.model;
      return runAgent(panel, workspaceRoot, task, runOutput, runExtensionRoot, getBackendProjectPath(), runModel);
    };
    const commandHandlers = createExtensionCommandHandlers({
      output,
      extensionRoot,
      resolveWorkspaceRoot,
      runAgent: runConfiguredAgent,
      getIsAgentRunning: () => isAgentRunning,
      setIsAgentRunning: value => {
        isAgentRunning = value === true;
      }
    });

    context.subscriptions.push(changedRangeDecoration, output, {
      dispose: () => editorNavigation.dispose()
    });
    editorNavigation.attachLifecycle(context);

    const createMessageHandler = webviewHost => createPanelMessageHandler({
      panel: webviewHost,
      output,
      extensionRoot,
      resolveWorkspaceRoot,
      runAgent: runConfiguredAgent,
      hasRunningProcess,
      stopCurrentAgent,
      editorNavigation,
      exportService,
      getModelSelectionState: () => buildModelSelectionStateWithPing(selectedModelState.model, selectedModelState.warning, selectedModelState.source),
      setSelectedModel: async candidate => {
        const previousModel = selectedModelState.model;
        const resolved = await saveSelectedOllamaModel(context.globalState, candidate);
        selectedModelState = resolved;
        if (resolved.warning) {
          output.appendLine(resolved.warning);
        }
        const payload = await buildModelSelectionStateWithPing(resolved.model, resolved.warning, resolved.source);
        if (resolved.model !== previousModel) {
          payload.notice = `Модель переключена: ${resolved.model}`;
        }
        return payload;
      },
      getIsAgentRunning: () => isAgentRunning,
      setIsAgentRunning: value => {
        isAgentRunning = value === true;
      }
    });

    const configureAgentWebview = webview => {
      webview.options = {
        enableScripts: true,
        localResourceRoots: [vscode.Uri.file(extensionRoot)]
      };
      webview.html = getHtml();
      webview.onDidReceiveMessage(createMessageHandler({ webview }));
    };

    const openAgentPanel = () => {
      const panel = vscode.window.createWebviewPanel(
        'localCursorAgent',
        'Local Cursor Agent',
        vscode.ViewColumn.Beside,
        { enableScripts: true, retainContextWhenHidden: true }
      );

      configureAgentWebview(panel.webview);
      return panel;
    };

    const openPanel = vscode.commands.registerCommand('localCursorAgent.openPanel', openAgentPanel);
    const showChat = vscode.commands.registerCommand('localCursorAgent.showChat', async () => {
      await vscode.commands.executeCommand('workbench.view.extension.localCursorAgent');
      await vscode.commands.executeCommand('localCursorAgent.chatView.focus');
    });

    const runTask = vscode.commands.registerCommand('localCursorAgent.runTask', () => commandHandlers.handleRunTask());
    const chatViewProvider = {
      resolveWebviewView(webviewView) {
        configureAgentWebview(webviewView.webview);
      }
    };

    context.subscriptions.push(
      openPanel,
      showChat,
      runTask,
      vscode.window.registerWebviewViewProvider('localCursorAgent.chatView', chatViewProvider, {
        webviewOptions: { retainContextWhenHidden: true }
      })
    );
    loadSelectedOllamaModel(context.globalState).then(resolved => {
      selectedModelState = resolved;
      if (resolved.warning) {
        output.appendLine(resolved.warning);
      }
    }).catch(err => {
      const message = err instanceof Error ? err.message : String(err);
      output.appendLine(`Model selection init failed: ${message}`);
    });
    output.appendLine('Local Cursor Agent activation completed');

    if (process.env.LOCAL_CURSOR_AGENT_AUTO_OPEN_PANEL === '1') {
      setTimeout(() => {
        try {
          vscode.commands.executeCommand('localCursorAgent.showChat');
          output.appendLine('Local Cursor Agent chat view opened automatically');
        } catch (err) {
          const message = err instanceof Error ? err.message : String(err);
          output.appendLine(`Local Cursor Agent chat auto-open failed: ${message}`);
          vscode.window.showErrorMessage(`Local Cursor Agent chat failed to open: ${message}`);
        }
      }, 500);
    }
  } catch (err) {
    const message = err instanceof Error
      ? (err.stack || err.message)
      : String(err);
    output.appendLine('Local Cursor Agent activation failed');
    output.appendLine(message);
    output.show(true);
    vscode.window.showErrorMessage(`Local Cursor Agent activation failed: ${err instanceof Error ? err.message : String(err)}`);
    throw err;
  }
}

function deactivate() {}

module.exports = { activate, deactivate };
