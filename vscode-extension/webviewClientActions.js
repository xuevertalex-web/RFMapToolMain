const webviewClientActions = `function startAgentRunFromInput() {
        if (uiRunning) {
          return;
        }

        const task = normalizeUserTaskInput(String(taskInput.value || '').trim());
        if (!task) {
          taskInput.value = '';
          autoResizeTaskInput();
          saveWebviewState();
          taskInput.focus();
          return;
        }
        const dialog = ensureActiveDialogForTask(task);
        activeRunDialogId = String(dialog.id || '');
        appendDialogMessage(activeRunDialogId, 'user', task);
        openDialogView(dialog);
        renderRecentRuns();
        saveWebviewState();

        const dispatchTask = buildDispatchTask(task);

        logs.textContent = '';
        suppressPlainResultLog = false;
        currentRunTask = task;
        logsCollapsed = false;
        updateLogsVisibility();
        status.textContent = 'Running...';
        if (resultSection) {
          resultSection.style.display = '';
        }
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
        const sessionContext = buildSessionMemoryContext(activeRunDialogId, task);
        vscode.postMessage({
          type: 'sendTask',
          task: dispatchTask,
          selectedModel: selectedOllamaModel,
          sessionContext
        });
      }

      function normalizeUserTaskInput(raw) {
        const task = String(raw || '').trim();
        if (!task) {
          return '';
        }
        const repaired = tryRepairMistypedKeyboardLayout(task);
        if (repaired.wasRepaired) {
          appendLogLine('system', 'Input normalized: keyboard layout mismatch auto-corrected.');
          status.textContent = 'Input corrected';
          return repaired.value;
        }
        return task;
      }

      function buildDispatchTask(task) {
        return String(task || '').trim();
      }

      function showLocalChatReply(task, replyText, badgeText, dialogId) {
        setRunningState(true);
        status.textContent = 'Thinking...';
        window.setTimeout(() => {
          setRunningState(false);
          currentRunTask = String(task || '').trim();
          status.textContent = 'Ready';
          result.textContent = String(replyText || '');
          resultBadge.textContent = String(badgeText || 'chat');
          resultBadge.className = 'result-badge running';
          if (resultSection) {
            resultSection.style.display = 'none';
          }
          updateCopyResultButton('Copy Result');
          lastResultPayload = { resultText: result.textContent, summaryText: '', buildText: '', changedFiles: [] };
          clearOutputButton.disabled = false;
          taskInput.value = '';
          autoResizeTaskInput();
          const activeId = String(dialogId || activeRunDialogId || '');
          const run = appendDialogMessage(activeId, 'assistant', result.textContent);
          if (run) {
            run.ok = true;
            run.resultText = result.textContent;
            run.summaryText = '';
            run.buildText = '';
            run.changedCount = 0;
            run.lastNormalizedRun = null;
            run.lastResultPayload = lastResultPayload;
            renderDialogThread(run);
          }
          renderRecentRuns();
          saveWebviewState();
          updateExportRunReportButtonState();
          updateExportLogsButtonState();
          activeRunDialogId = '';
        }, 350);
      }

      function tryRepairMistypedKeyboardLayout(input) {
        const value = String(input || '');
        const letters = (value.match(/[a-zA-Z\u0430-\u044f\u0410-\u042f\u0451\u0401]/g) || []);
        if (letters.length < 8) {
          return { wasRepaired: false, value };
        }

        const latinCount = (value.match(/[a-zA-Z]/g) || []).length;
        const cyrillicCount = (value.match(/[\u0430-\u044f\u0410-\u042f\u0451\u0401]/g) || []).length;
        if (cyrillicCount > 0 || latinCount < 6) {
          return { wasRepaired: false, value };
        }

        const keyboardMap = {
          q: '\\u0439', w: '\\u0446', e: '\\u0443', r: '\\u043a', t: '\\u0435', y: '\\u043d', u: '\\u0433', i: '\\u0448', o: '\\u0449', p: '\\u0437',
          '[': '\\u0445', ']': '\\u044a', a: '\\u0444', s: '\\u044b', d: '\\u0432', f: '\\u0430', g: '\\u043f', h: '\\u0440', j: '\\u043e', k: '\\u043b', l: '\\u0434',
          ';': '\\u0436', \"'\": '\\u044d', z: '\\u044f', x: '\\u0447', c: '\\u0441', v: '\\u043c', b: '\\u0438', n: '\\u0442', m: '\\u044c', ',': '\\u0431', '.': '\\u044e',
          '/': '.'
        };

        let converted = '';
        let convertedCount = 0;
        for (const ch of value) {
          const lower = ch.toLowerCase();
          const mapped = keyboardMap[lower];
          if (mapped) {
            converted += ch === lower ? mapped : mapped.toUpperCase();
            convertedCount++;
          } else {
            converted += ch;
          }
        }

        const ratio = convertedCount / Math.max(1, letters.length);
        if (ratio < 0.6) {
          return { wasRepaired: false, value };
        }
        return { wasRepaired: true, value: converted };
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
          // continue with next fallback
        }

        try {
          const textarea = document.createElement('textarea');
          textarea.value = value;
          textarea.setAttribute('readonly', 'readonly');
          textarea.style.position = 'fixed';
          textarea.style.left = '-9999px';
          textarea.style.top = '-9999px';
          document.body.appendChild(textarea);
          textarea.focus();
          textarea.select();
          const copied = document.execCommand('copy');
          document.body.removeChild(textarea);
          if (copied) {
            return;
          }
        } catch {
          // continue with extension-host clipboard
        }

        await copyViaHostClipboard(value);
      }

      const pendingClipboardRequests = new Map();
      let clipboardRequestCounter = 0;

      function handleCopyToClipboardResult(message) {
        const requestId = String(message && message.requestId || '');
        if (!requestId) {
          return false;
        }

        const pending = pendingClipboardRequests.get(requestId);
        if (!pending) {
          return false;
        }

        pendingClipboardRequests.delete(requestId);
        if (pending.timeoutId) {
          clearTimeout(pending.timeoutId);
        }

        if (message.ok) {
          pending.resolve();
        } else {
          pending.reject(new Error(String(message.error || 'clipboard_write_failed')));
        }

        return true;
      }

      async function copyViaHostClipboard(value) {
        const requestId = 'copy-' + String(++clipboardRequestCounter);
        await new Promise((resolve, reject) => {
          const timeoutId = window.setTimeout(() => {
            pendingClipboardRequests.delete(requestId);
            reject(new Error('clipboard_host_timeout'));
          }, 2500);

          pendingClipboardRequests.set(requestId, { resolve, reject, timeoutId });
          vscode.postMessage({
            type: 'copyToClipboard',
            text: value,
            requestId
          });
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




