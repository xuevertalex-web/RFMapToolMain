const webviewClientPersistence = `
      function updateLogsVisibility() {
        logs.style.display = '';
        updateExportLogsButtonState();
      }

      function toggleLogsVisibility() {
        updateLogsVisibility();
        saveWebviewState();
      }

      function updateCopyResultButton(text, resetAfterMs) {
        if (!copyResultButton) {
          return;
        }

        copyResultButton.textContent = text;
        if (Number.isFinite(resetAfterMs) && resetAfterMs > 0) {
          window.setTimeout(() => {
            copyResultButton.textContent = 'Copy Result';
          }, resetAfterMs);
        }
      }

      function clearOutput() {
        result.textContent = '';
        summary.textContent = '';
        buildStatus.textContent = 'Build: Not run';
        logs.textContent = '';
        status.textContent = 'Idle';
        clearRenderedChangedFiles();
        changedFilesTitle.textContent = 'Changed Files';
        resultBadge.textContent = '';
        resultBadge.className = 'result-badge';
        currentChangedKindFilter = 'All';
        changedKindFilter.value = 'All';
        currentChangedFiles = [];
        currentChangedHints = [];
        currentChangedRanges = [];
        currentKindMap = new Map();
        currentChangedRangeMap = new Map();
        currentChangedHintMap = new Map();
        currentRunTask = '';
        lastResultPayload = {
          resultText: '',
          summaryText: '',
          buildText: '',
          changedFiles: [],
          changedHints: [],
          changedRanges: [],
          changedKinds: [],
          isError: false,
          statusText: ''
        };
        structuredResultSection.style.display = 'none';
        updateCopyResultButton('Copy Result');
        updateCopyStructuredResultButton('Copy Structured Result');
        copyLogsButton.textContent = 'Копировать';
        saveWebviewState();
        updateRerunLastButton();
        updateCopyChangedFilesButton();
        updateExportChangedFilesButtonState();
        updateRunStats();
        updateCopyStructuredResultButton('Copy Structured Result');
        updateCopyStructuredResultButtonState();
        updateExportRunReportButtonState();
        updateExportLogsButtonState();
      }

      function autoResizeTaskInput() {
        if (!taskInput) {
          return;
        }

        taskInput.style.height = 'auto';
        taskInput.style.height = taskInput.scrollHeight + 'px';
      }

      function saveWebviewState() {
        vscode.setState({
          version: webviewStateVersion,
          taskInputValue: taskInput.value,
          changedKindFilterValue: currentChangedKindFilter,
          logsCollapsed: logsCollapsed
        });
      }

      function saveTaskInputState() {
        saveWebviewState();
      }`;

function getWebviewClientPersistence() {
  return webviewClientPersistence;
}

module.exports = { getWebviewClientPersistence };
