const webviewClientRunState = `
      function setRunningState(running) {
        uiRunning = running;
        sendButton.disabled = running;
        stopButton.disabled = !running;
        clearOutputButton.disabled = running;
        sendButton.textContent = running ? '...' : '↑';
        stopButton.textContent = 'Остановить';
        resultBadge.className = 'result-badge';
        resultBadge.textContent = '';
        if (running) {
          status.textContent = 'В работе';
          setModelStatusRunning();
          if (thinkingIndicator) {
            thinkingIndicator.style.display = 'flex';
          }
        } else {
          clearOutputButton.disabled = false;
          if (thinkingIndicator) {
            thinkingIndicator.style.display = 'none';
          }
        }
        renderRecentRuns();
        updateRerunLastButton();
        updateOpenAllChangedFilesButton();
        updateCopyChangedFilesButton();
        updateExportChangedFilesButtonState();
        updateRunStats();
        updateCopyStructuredResultButtonState();
        updateExportRunReportButtonState();
      }`;

function getWebviewClientRunState() {
  return webviewClientRunState;
}

module.exports = { getWebviewClientRunState };
