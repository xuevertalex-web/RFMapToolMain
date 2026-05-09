const webviewClientActions = `function startAgentRunFromInput() {
        if (uiRunning) {
          return;
        }

        const task = String(taskInput.value || '').trim();
        if (!task) {
          taskInput.value = '';
          autoResizeTaskInput();
          saveWebviewState();
          taskInput.focus();
          return;
        }

        logs.textContent = '';
        suppressPlainResultLog = false;
        currentRunTask = task;
        logsCollapsed = false;
        updateLogsVisibility();
        status.textContent = 'Running...';
        result.textContent = '';
        resultBadge.textContent = '';
        resultBadge.className = 'result-badge running';
        updateCopyResultButton('Copy Result');
        lastResultPayload = {
          resultText: '',
          summaryText: '',
          buildText: '',
          changedFiles: []
        };
        summary.textContent = '';
        summary.className = 'summary-box';
        clearRenderedChangedFiles();
        buildStatus.textContent = 'Build: Not run';
        buildStatus.className = 'build-status na';
        structuredResultSection.style.display = 'none';
        currentChangedKindFilter = 'All';
        changedKindFilter.value = 'All';
        currentChangedFiles = [];
        currentChangedHints = [];
        currentChangedRanges = [];
        currentKindMap = new Map();
        currentChangedRangeMap = new Map();
        currentChangedHintMap = new Map();
        clearOutputButton.disabled = true;
        setRunningState(true);
        taskInput.value = '';
        autoResizeTaskInput();
        saveWebviewState();
        updateExportRunReportButtonState();
        updateExportLogsButtonState();
        lastDispatchedRunModel = selectedOllamaModel;
        renderModelSelectionStatusLine();
        vscode.postMessage({
          type: 'sendTask',
          task,
          selectedModel: selectedOllamaModel
        });
      }

      function rerunLast() {
        const lastRun = getLastRerunnableRun();
        if (!lastRun) {
          return;
        }

        const task = String(lastRun.task || '').trim();
        if (!task || task === '(no task)' || uiRunning) {
          return;
        }

        taskInput.value = task;
        autoResizeTaskInput();
        saveWebviewState();
        taskInput.focus();
        startAgentRunFromInput();
      }

      function updateRerunLastButton() {
        if (!rerunLastButton) {
          return;
        }

        const lastRun = getLastRerunnableRun();
        const canRun = !!lastRun && !uiRunning;
        rerunLastButton.disabled = !canRun;
        rerunLastButton.style.opacity = canRun ? '1' : '0.55';
        rerunLastButton.style.cursor = canRun ? 'pointer' : 'not-allowed';
      }

      function openAllChangedFiles() {
        const files = getCurrentOpenableChangedFiles();
        if (!files.length || uiRunning || !lastResultPayload) {
          return;
        }

        vscode.postMessage({
          type: 'openAllChangedFiles',
          files
        });
      }

      function copyChangedFiles() {
        const files = getCurrentCopyableChangedFiles();
        if (!files.length || uiRunning || !lastResultPayload) {
          return;
        }

        const text = files.join('\\n');
        copyTextWithFallback(text).then(() => {
          copyChangedFilesButton.textContent = 'Copied';
          resetButtonTextLater(copyChangedFilesButton, 'Copy Changed Files', 1500);
        }).catch(() => {
          copyChangedFilesButton.textContent = 'Copy failed';
          resetButtonTextLater(copyChangedFilesButton, 'Copy Changed Files', 1500);
        });
      }

      function exportChangedFiles() {
        const files = getCurrentExportableChangedFiles();
        if (!files.length || uiRunning || !lastResultPayload) {
          return;
        }

        vscode.postMessage({
          type: 'exportChangedFiles',
          text: files.join('\\n')
        });
      }

      function resetButtonTextLater(button, originalText, resetAfterMs) {
        if (!button) {
          return;
        }

        const previousTimer = button._resetTextTimer || null;
        if (previousTimer) {
          clearTimeout(previousTimer);
          button._resetTextTimer = null;
        }

        if (Number.isFinite(resetAfterMs) && resetAfterMs > 0) {
          button._resetTextTimer = window.setTimeout(() => {
            button.textContent = originalText;
            button._resetTextTimer = null;
          }, resetAfterMs);
        }
      }

      function buildCopyResultText() {
        const chunks = [];

        if (lastResultPayload.resultText) {
          chunks.push('Result:\\n' + lastResultPayload.resultText);
        }

        if (lastResultPayload.summaryText) {
          chunks.push('Summary:\\n' + lastResultPayload.summaryText);
        }

        if (lastResultPayload.buildText) {
          chunks.push('Build:\\n' + lastResultPayload.buildText);
        }

        if (Array.isArray(lastResultPayload.changedFiles) && lastResultPayload.changedFiles.length > 0) {
          chunks.push('Changed Files:\\n' + lastResultPayload.changedFiles.map(file => '- ' + String(file)).join('\\n'));
        }

        return chunks.join('\\n\\n');
      }

      async function copyResult() {
        const text = buildCopyResultText();
        if (!text) {
          updateCopyResultButton('Copy failed', 1500);
          return;
        }

        try {
          await copyTextWithFallback(text);
          updateCopyResultButton('Copied', 1500);
        } catch {
          updateCopyResultButton('Copy failed', 1500);
        }
      }

      async function copyStructuredResult() {
        if (!hasStructuredResultPayload(lastResultPayload) || uiRunning) {
          return;
        }

        try {
          const text = JSON.stringify(lastResultPayload, null, 2);
          await copyTextWithFallback(text);
          updateCopyStructuredResultButton('Copied', 1500);
        } catch {
          updateCopyStructuredResultButton('Copy failed', 1500);
        }
      }

      function updateExportRunReportButtonState() {
        if (!exportRunReportButton) {
          return;
        }

        const canExport = !uiRunning && hasStructuredResultPayload(lastResultPayload);
        exportRunReportButton.disabled = !canExport;
        exportRunReportButton.style.opacity = canExport ? '1' : '0.55';
        exportRunReportButton.style.cursor = canExport ? 'pointer' : 'not-allowed';
      }

      function exportRunReport() {
        if (!hasStructuredResultPayload(lastResultPayload) || uiRunning) {
          return;
        }

        vscode.postMessage({
          type: 'exportRunReport',
          payload: lastResultPayload
        });
      }

      async function copyLogs() {
        const text = getLogsText();
        try {
          await copyTextWithFallback(text);
          copyLogsButton.textContent = 'Copied';
          resetButtonTextLater(copyLogsButton, 'Copy', 1500);
        } catch {
          copyLogsButton.textContent = 'Error';
          resetButtonTextLater(copyLogsButton, 'Copy', 1500);
        }
      }

      function updateExportLogsButtonState() {
        if (!exportLogsButton) {
          return;
        }

        const canExportLogs = !uiRunning && getLogsText().trim().length > 0;
        exportLogsButton.disabled = !canExportLogs;
        exportLogsButton.style.opacity = canExportLogs ? '1' : '0.55';
        exportLogsButton.style.cursor = canExportLogs ? 'pointer' : 'not-allowed';
      }

      function exportLogs() {
        const text = getLogsText();
        if (uiRunning || !text.trim()) {
          return;
        }

        vscode.postMessage({
          type: 'exportLogs',
          text
        });
      }

      function getLogsText() {
        const lines = Array.from(logs.querySelectorAll('.log-line'))
          .map(line => String(line.textContent || '').trimEnd())
          .filter(Boolean);
        return lines.length > 0 ? lines.join('\\n') : String(logs.textContent || '');
      }

      async function copyTextWithFallback(text) {
        const value = String(text || '');
        if (!value) {
          throw new Error('empty_copy_text');
        }

        try {
          if (navigator && navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
            await navigator.clipboard.writeText(value);
            return;
          }
        } catch {
          // fall through to extension-host clipboard
        }

        vscode.postMessage({
          type: 'copyToClipboard',
          text: value
        });
      }

      function updateCopyStructuredResultButton(text, resetAfterMs) {
        if (!copyStructuredResultButton) {
          return;
        }

        copyStructuredResultButton.textContent = text;
        if (Number.isFinite(resetAfterMs) && resetAfterMs > 0) {
          window.setTimeout(() => {
            copyStructuredResultButton.textContent = 'Copy Structured Result';
          }, resetAfterMs);
        }
      }

      function updateCopyStructuredResultButtonState() {
        if (!copyStructuredResultButton) {
          return;
        }

        const canCopy = !uiRunning && !!lastResultPayload;
        copyStructuredResultButton.disabled = !canCopy;
        copyStructuredResultButton.style.opacity = canCopy ? '1' : '0.55';
        copyStructuredResultButton.style.cursor = canCopy ? 'pointer' : 'not-allowed';
      }`;

function getWebviewClientActions() {
  return webviewClientActions;
}

module.exports = { getWebviewClientActions };

