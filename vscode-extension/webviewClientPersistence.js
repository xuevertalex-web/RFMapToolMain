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
        saveWebviewState();
        updateRerunLastButton();
        updateCopyChangedFilesButton();
        updateExportChangedFilesButtonState();
        updateRunStats();
        updateCopyStructuredResultButton('Copy Structured Result');
        updateCopyStructuredResultButtonState();
        updateExportRunReportButtonState();
        updateExportLogsButtonState();
        if (typeof renderDialogThread === 'function') {
          renderDialogThread(getSelectedDialogRun());
        }
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
          logsCollapsed: logsCollapsed,
          recentRuns: Array.isArray(recentRuns) ? recentRuns.slice(0, 20) : [],
          archivedRuns: Array.isArray(archivedRuns) ? archivedRuns.slice(0, 50) : [],
          sessionsViewMode: sessionsViewMode,
          dialogViewMode: dialogViewMode,
          selectedDialogId: String(selectedDialogId || '')
        });
      }

      function saveTaskInputState() {
        saveWebviewState();
      }`;

function getWebviewClientPersistence() {
  return webviewClientPersistence;
}

module.exports = { getWebviewClientPersistence };

