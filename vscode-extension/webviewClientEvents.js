const { getWebviewClientResultHandlers } = require('./webviewClientResultHandlers');

const webviewClientEvents = `
      if (helpButton) {
        helpButton.addEventListener('click', () => {
          appendLogLine('system', 'Help: Enter sends task, Shift+Enter adds a new line. Use New to start a new dialog.');
          logs.scrollTop = logs.scrollHeight;
        });
      }
      copyResultButton.addEventListener('click', copyResult);
      copyStructuredResultButton.addEventListener('click', copyStructuredResult);
      if (exportRunReportButton) {
        exportRunReportButton.addEventListener('click', exportRunReport);
      }
      clearOutputButton.addEventListener('click', () => {
        openEmptyDialogView();
        clearOutput();
      });
      if (backToDialogsButton) {
        backToDialogsButton.addEventListener('click', () => openDialogsListView());
      }
      if (sessionsTabActive) {
        sessionsTabActive.addEventListener('click', () => {
          sessionsViewMode = 'active';
          saveWebviewState();
          renderRecentRuns();
        });
      }
      if (sessionsTabArchived) {
        sessionsTabArchived.addEventListener('click', () => {
          sessionsViewMode = 'archived';
          saveWebviewState();
          renderRecentRuns();
        });
      }
      copyChangedFilesButton.addEventListener('click', copyChangedFiles);
      if (exportChangedFilesButton) {
        exportChangedFilesButton.addEventListener('click', exportChangedFiles);
      }
      openAllChangedFilesButton.addEventListener('click', openAllChangedFiles);
      if (rerunLastButton) {
        rerunLastButton.addEventListener('click', rerunLast);
      }
      updateLogsVisibility();
      updateRerunLastButton();
      updateOpenAllChangedFilesButton();
      updateCopyChangedFilesButton();
      updateExportChangedFilesButtonState();
      updateRunStats();
      updateCopyStructuredResultButtonState();
      updateExportRunReportButtonState();
      requestModelSelectionState();
      if (modelSelector) {
        modelSelector.addEventListener('change', handleModelSelectorChange);
      }

      document.getElementById('send').addEventListener('click', () => {
        startAgentRunFromInput();
      });

      stopButton.addEventListener('click', () => {
        if (!uiRunning) {
          return;
        }

        vscode.postMessage({ type: 'stopAgent' });
      });

      taskInput.addEventListener('keydown', event => {
        const isEnter = event.key === 'Enter' || event.code === 'Enter' || event.keyCode === 13;
        if (event.isComposing) {
          return;
        }
        if (isEnter && !event.shiftKey) {
          event.preventDefault();
          startAgentRunFromInput();
          return;
        }

        if (event.key === 'Escape') {
          event.preventDefault();
        }
      });

      taskInput.addEventListener('input', autoResizeTaskInput);
      taskInput.addEventListener('input', saveTaskInputState);
      autoResizeTaskInput();

      changedKindFilter.addEventListener('change', () => {
        currentChangedKindFilter = changedKindFilter.value || 'All';
        saveWebviewState();
        renderChangedFiles();
        updateRunStats();
      });

      window.addEventListener('message', event => {
        const message = event.data;
        if (message.type === 'agentLog') {
          appendLogChunk(message.stream === 'stderr' ? 'stderr' : 'stdout', message.text);
          logs.scrollTop = logs.scrollHeight;
        } else if (message.type === 'log') {
          appendLogLine('system', message.line);
        } else if (message.type === 'result') {
          result.textContent = message.text || '';
        } else if (message.type === 'agentFinished') {
          handleAgentFinishedMessage(message);
        } else if (message.type === 'runningState') {
          setRunningState(!!message.running);
        } else if (message.type === 'modelSelectionState') {
          applyModelSelectionState(message.payload);
        } else if (message.type === 'copyToClipboardResult') {
          handleCopyToClipboardResult(message);
        }
      });`;

function getWebviewClientEvents() {
  return [
    getWebviewClientResultHandlers(),
    webviewClientEvents
  ].join('\n\n');
}

module.exports = { getWebviewClientEvents };
