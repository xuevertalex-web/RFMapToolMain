const webviewClientResultHandlers = `function applyNormalizedRunResult(run, options) {
        const opts = options && typeof options === 'object' ? options : {};
        if (resultSection) {
          resultSection.style.display = '';
        }
        structuredResultSection.style.display = 'block';
        status.textContent = run.status === 'error'
          ? 'Error'
          : run.status === 'fallback-success'
            ? 'Fallback success'
            : run.status === 'success'
              ? 'Success'
              : 'Running';
        renderRunStatus(run);
        renderModelStatus(run);
        renderRunSummary(run);
        renderFailureSummary(run);
        renderRunTimeline(run);
        renderBuildDiagnostics(run);

        currentChangedFiles = sortChangedFilesByKind(run.changedFiles, buildKindMap(run.changedKinds));
        currentChangedHints = run.changedHints;
        currentChangedRanges = run.changedRanges;
        currentKindMap = buildKindMap(run.changedKinds);
        currentChangedHintMap = buildHintMap(run.changedHints);
        currentChangedRangeMap = buildRangeMap(run.changedRanges);
        renderChangedFiles();

        lastResultPayload = {
          resultText: run.messageText || '',
          summaryText: run.summary || '',
          buildText: run.buildText || '',
          changedFiles: currentChangedFiles.map(fileInfo => String(fileInfo.path)),
          changedHints: currentChangedHints,
          changedRanges: currentChangedRanges,
          changedKinds: run.changedKinds,
          diagnostics: run.diagnostics,
          timeline: run.timeline,
          failure: run.failure,
          isError: run.failed,
          statusText: status.textContent
        };

        const dialogId = String(opts.dialogId || activeRunDialogId || selectedDialogId || '');
        if (dialogId) {
          recordDialogResult(dialogId, run, lastResultPayload);
          const selectedRun = getSelectedDialogRun();
          if (selectedRun) {
            renderDialogThread(selectedRun);
          }
        } else if (!opts.skipHistory) {
          addRecentRun({
            timestamp: new Date().toLocaleString(),
            task: run.task,
            ok: run.ok,
            resultText: lastResultPayload.resultText,
            summaryText: lastResultPayload.summaryText,
            buildText: lastResultPayload.buildText,
            changedCount: currentChangedFiles.length
          });
        }

        const visibleFiles = getFilteredChangedFiles();
        if (!opts.skipAutoOpen && run.ok && visibleFiles.length === 1) {
          const autoOpenPath = String(visibleFiles[0].path);
          const autoRange = currentChangedRangeMap.get(normalizeFileKey(autoOpenPath)) || null;
          vscode.postMessage({
            type: 'openFile',
            path: autoOpenPath,
            startLine: autoRange ? autoRange.startLine : undefined,
            endLine: autoRange ? autoRange.endLine : undefined,
            silent: true
          });
          highlightOpenedChangedFile(autoOpenPath);
        }

        logsCollapsed = false;
        renderRawLogs();
        saveWebviewState();
        renderRunActions(run);
        updateRunStats();
        currentRunTask = '';
        activeRunDialogId = '';
        renderRecentRuns();
      }

      function handleAgentFinishedMessage(message) {
        setRunningState(false);
        const run = normalizeRunResult(message || {});
        applyNormalizedRunResult(run);
      }`;

function getWebviewClientResultHandlers() {
  return webviewClientResultHandlers;
}

module.exports = { getWebviewClientResultHandlers };

