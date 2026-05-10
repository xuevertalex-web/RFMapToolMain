const webviewClientRecentRunsState = `
      function getRecentRunDisplayTask(task) {
        const value = String(task || '').trim();
        return value || '(no task)';
      }

      function getRecentRunBadgeClass(ok) {
        return ok ? 'ok' : 'error';
      }

      function normalizeDialogMessage(message) {
        if (!message || typeof message !== 'object') {
          return null;
        }
        const role = String(message.role || '').trim();
        const text = String(message.text || '').trim();
        if (!text || (role !== 'user' && role !== 'assistant')) {
          return null;
        }
        return {
          role,
          text,
          timestamp: String(message.timestamp || '').trim() || new Date().toLocaleString()
        };
      }

      function buildFallbackMessages(task, resultText) {
        const messages = [];
        const taskValue = String(task || '').trim();
        if (taskValue && taskValue !== '(no task)') {
          messages.push({ role: 'user', text: taskValue, timestamp: new Date().toLocaleString() });
        }
        const resultValue = String(resultText || '').trim();
        if (resultValue) {
          messages.push({ role: 'assistant', text: resultValue, timestamp: new Date().toLocaleString() });
        }
        return messages;
      }

      function normalizeDialogRun(run) {
        const item = run && typeof run === 'object' ? run : {};
        const task = getRecentRunDisplayTask(item.task);
        const resultText = String(item.resultText || '').trim();
        const normalizedMessages = Array.isArray(item.messages)
          ? item.messages.map(normalizeDialogMessage).filter(Boolean)
          : [];
        const messages = normalizedMessages.length > 0
          ? normalizedMessages
          : buildFallbackMessages(task, resultText);
        return {
          id: String(item.id || (String(Date.now()) + '-' + Math.random().toString(36).slice(2, 8))),
          timestamp: String(item.timestamp || '').trim() || new Date().toLocaleString(),
          task,
          ok: !!item.ok,
          resultText,
          summaryText: String(item.summaryText || '').trim(),
          buildText: String(item.buildText || '').trim(),
          changedCount: Number.isFinite(item.changedCount) ? Math.max(0, Math.floor(item.changedCount)) : 0,
          messages,
          lastNormalizedRun: item.lastNormalizedRun && typeof item.lastNormalizedRun === 'object' ? item.lastNormalizedRun : null,
          lastResultPayload: item.lastResultPayload && typeof item.lastResultPayload === 'object' ? item.lastResultPayload : null
        };
      }

      function migrateRecentRunCollections() {
        recentRuns = Array.isArray(recentRuns) ? recentRuns.map(normalizeDialogRun).slice(0, 20) : [];
        archivedRuns = Array.isArray(archivedRuns) ? archivedRuns.map(normalizeDialogRun).slice(0, 50) : [];
      }

      function canRerunRecentTask(run) {
        const task = String(run && run.task ? run.task : '').trim();
        return !!task && task !== '(no task)' && !uiRunning;
      }

      function getLastRerunnableRun() {
        if (!Array.isArray(recentRuns) || recentRuns.length === 0) {
          return null;
        }
        for (const run of recentRuns) {
          const task = String(run && run.task ? run.task : '').trim();
          if (task && task !== '(no task)') return run;
        }
        return null;
      }

      function truncateRecentRunTask(task) {
        const value = String(task || '').trim() || '(no task)';
        const maxLength = 56;
        if (value.length <= maxLength) return value;
        return value.slice(0, maxLength - 3).trimEnd() + '...';
      }

      function findRunInCollectionsById(runId) {
        const id = String(runId || '').trim();
        if (!id) {
          return { list: null, index: -1, run: null };
        }
        const recentIndex = recentRuns.findIndex(item => String(item.id || '') === id);
        if (recentIndex >= 0) {
          return { list: recentRuns, index: recentIndex, run: recentRuns[recentIndex] };
        }
        const archivedIndex = archivedRuns.findIndex(item => String(item.id || '') === id);
        if (archivedIndex >= 0) {
          return { list: archivedRuns, index: archivedIndex, run: archivedRuns[archivedIndex] };
        }
        return { list: null, index: -1, run: null };
      }

      function getSelectedDialogRun() {
        const info = findRunInCollectionsById(selectedDialogId);
        return info && info.run ? info.run : null;
      }

      function buildSessionMemoryContext(runId, currentTask) {
        const info = findRunInCollectionsById(runId);
        const run = info && info.run ? info.run : null;
        if (!run) {
          return null;
        }

        const profile = {
          recentMessages: 30,
          maxPromptCharsFromHistory: 6000
        };

        const allMessages = Array.isArray(run.messages)
          ? run.messages.map(normalizeDialogMessage).filter(Boolean)
          : [];
        const recent = allMessages.slice(-profile.recentMessages);
        const older = allMessages.slice(0, Math.max(0, allMessages.length - recent.length));

        const recentTrimmed = [];
        let usedChars = 0;
        for (let i = recent.length - 1; i >= 0; i--) {
          const item = recent[i];
          const text = String(item.text || '').trim().slice(0, 900);
          if (!text) {
            continue;
          }
          const role = item.role === 'assistant' ? 'assistant' : 'user';
          const packed = { role, text, timestamp: String(item.timestamp || '') };
          const estimated = role.length + text.length + packed.timestamp.length + 12;
          if (usedChars + estimated > profile.maxPromptCharsFromHistory) {
            break;
          }
          usedChars += estimated;
          recentTrimmed.unshift(packed);
        }

        const olderTail = older.slice(-8).map(item => {
          const role = item.role === 'assistant' ? 'assistant' : 'user';
          const text = String(item.text || '').trim().slice(0, 120);
          return text ? (role + ': ' + text) : '';
        }).filter(Boolean);
        const historySummary = older.length > 0
          ? ('Older messages: ' + String(older.length) + '. Key tail: ' + olderTail.join(' | '))
          : '';

        const last = run.lastNormalizedRun && typeof run.lastNormalizedRun === 'object'
          ? run.lastNormalizedRun
          : null;
        const sessionState = {
          lastTask: String(run.task || '').trim(),
          lastStatus: last ? String(last.status || last.finalStatus || '').trim() : (run.ok ? 'success' : 'error'),
          changedFilesCount: Number.isFinite(run.changedCount) ? Number(run.changedCount) : 0,
          nextActions: last && Array.isArray(last.nextActionCandidates)
            ? last.nextActionCandidates.map(x => String(x || '').trim()).filter(Boolean).slice(0, 3)
            : []
        };

        const payload = run.lastResultPayload && typeof run.lastResultPayload === 'object'
          ? run.lastResultPayload
          : null;
        const lastStructuredResultSummary = payload
          ? {
              statusText: String(payload.statusText || '').trim(),
              summaryText: String(payload.summaryText || '').trim().slice(0, 500),
              resultText: String(payload.resultText || '').trim().slice(0, 500),
              changedFilesCount: Array.isArray(payload.changedFiles) ? payload.changedFiles.length : 0
            }
          : null;

        return {
          enabled: true,
          profile,
          currentTask: String(currentTask || '').trim(),
          historySummary,
          recentMessages: recentTrimmed,
          sessionState,
          lastStructuredResultSummary
        };
      }

      function renderDialogThread(run) {
        if (!dialogThread) {
          return;
        }
        dialogThread.replaceChildren();
        if (!run) {
          return;
        }

        const messages = Array.isArray(run.messages) ? run.messages : [];
        if (messages.length === 0) {
          const empty = document.createElement('div');
          empty.className = 'thread-empty';
          empty.textContent = 'Start this dialog by sending a message.';
          dialogThread.appendChild(empty);
          return;
        }

        for (const message of messages) {
          const item = document.createElement('article');
          item.className = 'thread-message ' + (message.role === 'user' ? 'thread-message-user' : 'thread-message-assistant');

          const meta = document.createElement('div');
          meta.className = 'thread-message-meta';
          meta.textContent = (message.role === 'user' ? 'You' : 'Agent') + ' · ' + String(message.timestamp || '');
          item.appendChild(meta);

          const body = document.createElement('div');
          body.className = 'thread-message-body';
          body.textContent = String(message.text || '');
          item.appendChild(body);

          dialogThread.appendChild(item);
        }
      }

      function restoreRecentRunSession(run) {
        if (!run) return;
        const normalized = normalizeDialogRun(run);
        currentRunTask = String(normalized.task || '').trim();
        if (normalized.lastNormalizedRun && typeof applyNormalizedRunResult === 'function') {
          if (resultSection) {
            resultSection.style.display = '';
          }
          applyNormalizedRunResult(normalized.lastNormalizedRun, { skipHistory: true, skipAutoOpen: true, dialogId: normalized.id });
        } else {
          result.textContent = String(normalized.resultText || '').trim() || 'No result text';
          summary.textContent = String(normalized.summaryText || '').trim();
          buildStatus.textContent = normalized.buildText ? ('Build: ' + String(normalized.buildText)) : 'Build: Not run';
          status.textContent = normalized.ok ? 'Success' : 'Error';
          resultBadge.textContent = normalized.ok ? 'OK' : 'Error';
          resultBadge.className = 'result-badge ' + getRecentRunBadgeClass(normalized.ok);
          structuredResultSection.style.display = 'none';
          if (resultSection) {
            resultSection.style.display = 'none';
          }
        }
        taskInput.value = '';
        autoResizeTaskInput();
        renderDialogThread(normalized);
        saveWebviewState();
      }

      function createDialogSession(initialTask) {
        const item = normalizeDialogRun({
          timestamp: new Date().toLocaleString(),
          task: getRecentRunDisplayTask(initialTask || ''),
          ok: true,
          resultText: '',
          summaryText: '',
          buildText: '',
          changedCount: 0,
          messages: []
        });
        recentRuns.unshift(item);
        if (recentRuns.length > 20) recentRuns = recentRuns.slice(0, 20);
        return item;
      }

      function ensureActiveDialogForTask(task) {
        const current = getSelectedDialogRun();
        if (current && recentRuns.includes(current)) {
          return current;
        }
        const created = createDialogSession(task);
        selectedDialogId = String(created.id || '');
        return created;
      }

      function appendDialogMessage(runId, role, text) {
        const info = findRunInCollectionsById(runId);
        if (!info.run) {
          return null;
        }
        const value = String(text || '').trim();
        if (!value) {
          return info.run;
        }
        if (!Array.isArray(info.run.messages)) {
          info.run.messages = [];
        }
        info.run.messages.push({
          role: role === 'assistant' ? 'assistant' : 'user',
          text: value,
          timestamp: new Date().toLocaleString()
        });
        info.run.timestamp = new Date().toLocaleString();
        if (role === 'user') {
          info.run.task = getRecentRunDisplayTask(value);
        }
        if (Array.isArray(info.list) && info.list === archivedRuns) {
          const [archivedRun] = archivedRuns.splice(info.index, 1);
          recentRuns.unshift(archivedRun);
          if (recentRuns.length > 20) recentRuns = recentRuns.slice(0, 20);
          selectedDialogId = String(archivedRun.id || '');
          return archivedRun;
        }
        return info.run;
      }

      function recordDialogResult(runId, run, payload) {
        const info = findRunInCollectionsById(runId);
        if (!info.run || !run) {
          return;
        }
        info.run.ok = !!run.ok;
        info.run.resultText = String(run.messageText || '').trim();
        info.run.summaryText = String(run.summary || '').trim();
        info.run.buildText = String(run.buildText || '').trim();
        info.run.changedCount = Array.isArray(run.changedFiles) ? run.changedFiles.length : 0;
        info.run.timestamp = new Date().toLocaleString();
        info.run.lastNormalizedRun = run;
        info.run.lastResultPayload = payload && typeof payload === 'object' ? payload : null;

        const messages = Array.isArray(info.run.messages) ? info.run.messages : [];
        const last = messages.length > 0 ? messages[messages.length - 1] : null;
        const resultText = String(info.run.resultText || '').trim();
        if (resultText && !(last && last.role === 'assistant' && String(last.text || '').trim() === resultText)) {
          messages.push({ role: 'assistant', text: resultText, timestamp: new Date().toLocaleString() });
        }
        info.run.messages = messages;
      }

      function addRecentRun(entry) {
        const item = normalizeDialogRun(entry);
        recentRuns.unshift(item);
        if (recentRuns.length > 20) recentRuns = recentRuns.slice(0, 20);
        saveWebviewState();
        renderRecentRuns();
        updateRerunLastButton();
      }

      function resolveRunIndex(items, runId, task, timestamp) {
        const id = String(runId || '').trim();
        if (id) {
          const indexById = items.findIndex(item => String(item.id || '') === id);
          if (indexById >= 0) return indexById;
        }
        const fallbackTask = String(task || '').trim();
        const fallbackTimestamp = String(timestamp || '').trim();
        if (!fallbackTask && !fallbackTimestamp) return -1;
        return items.findIndex(item =>
          String(item.task || '').trim() === fallbackTask &&
          String(item.timestamp || '').trim() === fallbackTimestamp
        );
      }

      function archiveRecentRunById(runId, task, timestamp) {
        const id = String(runId || '').trim();
        const index = resolveRunIndex(recentRuns, id, task, timestamp);
        if (index < 0) return;
        const [run] = recentRuns.splice(index, 1);
        archivedRuns.unshift(run);
        if (archivedRuns.length > 50) archivedRuns = archivedRuns.slice(0, 50);
        if (String(selectedDialogId || '') === String(run.id || '')) {
          openDialogsListView();
        }
        saveWebviewState();
        renderRecentRuns();
      }

      function restoreArchivedRunById(runId, task, timestamp) {
        const id = String(runId || '').trim();
        const index = resolveRunIndex(archivedRuns, id, task, timestamp);
        if (index < 0) return;
        const [run] = archivedRuns.splice(index, 1);
        recentRuns.unshift(run);
        if (recentRuns.length > 20) recentRuns = recentRuns.slice(0, 20);
        saveWebviewState();
        renderRecentRuns();
      }

      function deleteRunById(runId, task, timestamp) {
        const id = String(runId || '').trim();
        const recentIndex = resolveRunIndex(recentRuns, id, task, timestamp);
        if (recentIndex >= 0) {
          const run = recentRuns[recentIndex];
          recentRuns.splice(recentIndex, 1);
          if (String(selectedDialogId || '') === String(run && run.id || '')) {
            openDialogsListView();
          }
        }
        const archivedIndex = resolveRunIndex(archivedRuns, id, task, timestamp);
        if (archivedIndex >= 0) {
          archivedRuns.splice(archivedIndex, 1);
        }
        saveWebviewState();
        renderRecentRuns();
      }
`;

function getWebviewClientRecentRunsState() {
  return webviewClientRecentRunsState;
}

module.exports = { getWebviewClientRecentRunsState };

