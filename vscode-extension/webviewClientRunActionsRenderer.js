const webviewClientRunActionsRenderer = `function renderRunActions(run) {
        updateCopyChangedFilesButton();
        updateExportChangedFilesButtonState();
        updateOpenAllChangedFilesButton();
        updateCopyStructuredResultButtonState();
        updateExportRunReportButtonState();
        updateExportLogsButtonState();
        if (copyResultButton) {
          copyResultButton.disabled = !run.messageText;
        }
      }

      function renderRawLogs() {
        updateLogsVisibility();
        updateExportLogsButtonState();
      }`;

function getWebviewClientRunActionsRenderer() {
  return webviewClientRunActionsRenderer;
}

module.exports = { getWebviewClientRunActionsRenderer };
