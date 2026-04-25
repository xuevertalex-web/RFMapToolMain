const webviewClientChangedFiles = `function normalizeChangedFileEntry(entry) {
        if (entry && typeof entry === 'object') {
          const pathValue = entry.path || entry.filePath || entry.file || '';
          const statusValue = normalizeStatus(entry.status || entry.changeType || entry.kind);
          return {
            path: String(pathValue || ''),
            status: statusValue
          };
        }

        const value = String(entry || '');
        const inferred = inferStatusFromPath(value);
        return {
          path: value,
          status: inferred
        };
      }

      function clearRenderedChangedFiles() {
        if (changedFiles) {
          changedFiles.replaceChildren();
        }
        if (changedFilesTitle) {
          changedFilesTitle.textContent = 'Changed Files';
        }
      }

      function getFilteredChangedFiles() {
        if (!Array.isArray(currentChangedFiles) || currentChangedFiles.length === 0) {
          return [];
        }

        const selectedFilter = String(currentChangedKindFilter || 'All').trim();
        if (!selectedFilter || selectedFilter === 'All') {
          return currentChangedFiles.slice();
        }

        return currentChangedFiles.filter(fileInfo => {
          const kind = currentKindMap.get(normalizeFileKey(fileInfo.path)) || 'Unknown';
          return normalizeKind(kind) === selectedFilter;
        });
      }

      function getCurrentOpenableChangedFiles() {
        const filteredFiles = getFilteredChangedFiles();
        return filteredFiles.map(fileInfo => {
          const fileKey = normalizeFileKey(fileInfo.path);
          const range = currentChangedRangeMap.get(fileKey) || null;
          return {
            path: String(fileInfo.path),
            startLine: range ? range.startLine : undefined,
            endLine: range ? range.endLine : undefined
          };
        });
      }

      function getCurrentCopyableChangedFiles() {
        return getFilteredChangedFiles().map(fileInfo => String(fileInfo.path));
      }

      function getCurrentExportableChangedFiles() {
        return getCurrentCopyableChangedFiles();
      }

      function getRunStatsSummary() {
        const filteredFiles = getFilteredChangedFiles();
        const changedFilesCount = filteredFiles.length;
        const selectedFilter = String(currentChangedKindFilter || 'All').trim() || 'All';
        const buildText = String(lastResultPayload && lastResultPayload.buildText ? lastResultPayload.buildText : '').trim();
        const buildStatusText = buildText || 'Unknown';
        return {
          changedFilesCount,
          hintCount: filteredFiles.reduce((count, fileInfo) => {
            const key = normalizeFileKey(fileInfo.path);
            return count + (currentChangedHintMap.has(key) ? 1 : 0);
          }, 0),
          rangeCount: filteredFiles.reduce((count, fileInfo) => {
            const key = normalizeFileKey(fileInfo.path);
            return count + (currentChangedRangeMap.has(key) ? 1 : 0);
          }, 0),
          buildStatusText,
          selectedFilter
        };
      }

      function updateRunStats() {
        if (!runStats) {
          return;
        }

        const stats = getRunStatsSummary();
        runStats.className = 'summary-box ' + (uiRunning ? 'warn' : (lastResultPayload && lastResultPayload.isError ? 'error' : 'na'));
        runStats.textContent = [
          'Files: ' + stats.changedFilesCount,
          'Hints: ' + stats.hintCount,
          'Ranges: ' + stats.rangeCount,
          'Build: ' + stats.buildStatusText,
          'Filter: ' + stats.selectedFilter
        ].join('\\n');
      }

      function updateCopyChangedFilesButton() {
        if (!copyChangedFilesButton) {
          return;
        }

        const canCopy = !uiRunning && !!lastResultPayload && getFilteredChangedFiles().length > 0;
        copyChangedFilesButton.disabled = !canCopy;
        copyChangedFilesButton.style.opacity = canCopy ? '1' : '0.55';
        copyChangedFilesButton.style.cursor = canCopy ? 'pointer' : 'not-allowed';
      }

      function updateExportChangedFilesButtonState() {
        if (!exportChangedFilesButton) {
          return;
        }

        const canExport = !uiRunning && !!lastResultPayload && getFilteredChangedFiles().length > 0;
        exportChangedFilesButton.disabled = !canExport;
        exportChangedFilesButton.style.opacity = canExport ? '1' : '0.55';
        exportChangedFilesButton.style.cursor = canExport ? 'pointer' : 'not-allowed';
      }

      function updateCopyStructuredResultButtonState() {
        if (!copyStructuredResultButton) {
          return;
        }

        const canCopy = !uiRunning && hasStructuredResultPayload(lastResultPayload);
        copyStructuredResultButton.disabled = !canCopy;
        copyStructuredResultButton.style.opacity = canCopy ? '1' : '0.55';
        copyStructuredResultButton.style.cursor = canCopy ? 'pointer' : 'not-allowed';
      }

      function hasStructuredResultPayload(payload) {
        if (!payload || typeof payload !== 'object') {
          return false;
        }

        return !!(
          String(payload.resultText || '').trim() ||
          String(payload.summaryText || '').trim() ||
          String(payload.buildText || '').trim() ||
          (Array.isArray(payload.changedFiles) && payload.changedFiles.length > 0) ||
          (Array.isArray(payload.changedHints) && payload.changedHints.length > 0) ||
          (Array.isArray(payload.changedRanges) && payload.changedRanges.length > 0) ||
          (Array.isArray(payload.changedKinds) && payload.changedKinds.length > 0) ||
          payload.isError
        );
      }

      function updateOpenAllChangedFilesButton() {
        if (!openAllChangedFilesButton) {
          return;
        }

        const canOpen = !uiRunning && !!lastResultPayload && getFilteredChangedFiles().length > 0;
        openAllChangedFilesButton.disabled = !canOpen;
        openAllChangedFilesButton.style.opacity = canOpen ? '1' : '0.55';
        openAllChangedFilesButton.style.cursor = canOpen ? 'pointer' : 'not-allowed';
      }

      function renderChangedFiles() {
        clearRenderedChangedFiles();
        updateOpenAllChangedFilesButton();
        updateExportChangedFilesButtonState();
        updateRunStats();

        if (!Array.isArray(currentChangedFiles) || currentChangedFiles.length === 0) {
          const emptyItem = document.createElement('li');
          emptyItem.textContent = 'No changed files';
          changedFiles.appendChild(emptyItem);
          return;
        }

        const filteredFiles = getFilteredChangedFiles();
        changedFilesTitle.textContent = 'Changed Files (' + filteredFiles.length + ')';

        if (filteredFiles.length === 0) {
          const emptyItem = document.createElement('li');
          emptyItem.textContent = 'No files for selected filter';
          changedFiles.appendChild(emptyItem);
          return;
        }

        for (const fileInfo of filteredFiles) {
          const fileKey = normalizeFileKey(fileInfo.path);
          const item = document.createElement('li');
          item.style.cursor = 'pointer';
          item.title = 'Open file';
          item.setAttribute('aria-label', 'Open file ' + String(fileInfo.path));
          item.dataset.filePath = String(fileInfo.path);
          item.dataset.status = fileInfo.status;
          item.innerHTML = '<code>' + escapeHtml(String(fileInfo.path)) + '</code>' +
            '<span class="status-badge ' + escapeHtml(String(fileInfo.status)) + '">' + escapeHtml(String(fileInfo.status)) + '</span>';

          const kind = currentKindMap.get(fileKey) || 'Unknown';
          if (kind && normalizeKind(kind) !== 'Unknown') {
            const kindBadge = document.createElement('span');
            const kindStyle = getChangedKindBadgeStyle(kind);
            kindBadge.className = 'kind-badge ' + kindStyle.className;
            kindBadge.style.background = kindStyle.background;
            kindBadge.style.borderColor = kindStyle.borderColor;
            kindBadge.style.color = kindStyle.color;
            kindBadge.textContent = '[' + kind + ']';
            item.appendChild(kindBadge);
          }

          const hint = currentChangedHintMap.get(fileKey);
          if (hint) {
            const hintLine = document.createElement('div');
            hintLine.style.marginTop = '4px';
            hintLine.style.fontSize = '0.85em';
            hintLine.style.opacity = '0.8';
            hintLine.textContent = '-> ' + hint;
            item.appendChild(hintLine);
          }

          item.addEventListener('click', () => {
            const range = currentChangedRangeMap.get(fileKey) || null;
            vscode.postMessage({
              type: 'openFile',
              path: String(fileInfo.path),
              startLine: range ? range.startLine : undefined,
              endLine: range ? range.endLine : undefined
            });
          });

          changedFiles.appendChild(item);
        }
      }

      function buildHintMap(hints) {
        const map = new Map();
        if (!Array.isArray(hints)) {
          return map;
        }

        for (const hintEntry of hints) {
          if (!hintEntry || typeof hintEntry !== 'object') {
            continue;
          }

          const file = normalizeFileKey(hintEntry.file || hintEntry.path || hintEntry.filePath || '');
          const hint = String(hintEntry.hint || hintEntry.text || '').trim();
          if (file && hint) {
            map.set(file, hint);
          }
        }

        return map;
      }

      function buildRangeMap(ranges) {
        const map = new Map();
        if (!Array.isArray(ranges)) {
          return map;
        }

        for (const rangeEntry of ranges) {
          if (!rangeEntry || typeof rangeEntry !== 'object') {
            continue;
          }

          const file = normalizeFileKey(rangeEntry.file || rangeEntry.path || rangeEntry.filePath || '');
          const startLine = normalizeLineNumber(rangeEntry.startLine);
          const endLine = normalizeLineNumber(rangeEntry.endLine);
          if (file && startLine !== null) {
            map.set(file, {
              startLine,
              endLine: endLine !== null ? Math.max(startLine, endLine) : startLine
            });
          }
        }

        return map;
      }

      function buildKindMap(kinds) {
        const map = new Map();
        if (!Array.isArray(kinds)) {
          return map;
        }

        for (const kindEntry of kinds) {
          if (!kindEntry || typeof kindEntry !== 'object') {
            continue;
          }

          const file = normalizeFileKey(kindEntry.file || kindEntry.path || kindEntry.filePath || '');
          const kind = normalizeKind(kindEntry.kind || kindEntry.type || '');
          if (file && kind) {
            map.set(file, kind);
          }
        }

        return map;
      }

      function sortChangedFilesByKind(files, kindMap) {
        const decorated = files.map((file, index) => ({
          file,
          index,
          priority: getChangedKindPriority(kindMap.get(normalizeFileKey(file.path)) || 'Unknown')
        }));

        decorated.sort((a, b) => {
          if (a.priority !== b.priority) {
            return a.priority - b.priority;
          }
          return a.index - b.index;
        });

        return decorated.map(item => item.file);
      }`;

function getWebviewClientChangedFiles() {
  return webviewClientChangedFiles;
}

module.exports = { getWebviewClientChangedFiles };
