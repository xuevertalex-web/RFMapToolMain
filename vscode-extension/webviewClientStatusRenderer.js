const webviewClientStatusRenderer = `function renderRunStatus(run) {
        if (!runStatusGrid) return;
        runStatusGrid.replaceChildren();

        const fallbackActive = run.status === 'fallback-success' || !!run.fallbackReason || !!run.fallbackMode;
        const modelUsed = normalizeStatusCell(run.modelUsed, '') || normalizeStatusCell([run.provider, run.model].filter(Boolean).join(' / '), 'not available');
        const finalStatus = normalizeStatusCell(run.finalStatus, '');
        const normalizedFinalStatus = finalStatus ? finalStatus.toLowerCase() : '';
        const changedFilesCount = Number.isFinite(run.changedFilesCount)
          ? run.changedFilesCount
          : Array.isArray(run.changedFiles)
            ? run.changedFiles.length
            : 0;
        const embeddingsSummary = normalizeStatusCell(run.embeddingsSummary, '') || normalizeStatusCell(run.embeddingsStatus, 'not available');

        const rows = [
          ['status', run.status || 'not available'],
          ['reason code', normalizeStatusCell(run.reasonCode, 'not available')],
          ['continuation hint', normalizeStatusCell(run.continuationHint, 'not available')],
          ['next actions', Array.isArray(run.nextActionCandidates) && run.nextActionCandidates.length ? run.nextActionCandidates.join(' | ') : 'not available']
        ];

        if (finalStatus && normalizedFinalStatus !== String(run.status || '').toLowerCase()) {
          rows.push(['final status', finalStatus]);
        }

        if (fallbackActive) {
          rows.push(['fallback reason', normalizeStatusCell(run.fallbackReason, 'not available')]);
          rows.push(['fallback mode', normalizeStatusCell(run.fallbackMode, 'not available')]);
        }

        rows.push(['build', normalizeStatusCell(run.buildText, 'not run')]);
        rows.push(['changed files', String(Math.max(0, changedFilesCount))]);
        rows.push(['approval required', String(Math.max(0, Number.isFinite(run.approvalRequiredCount) ? run.approvalRequiredCount : 0))]);
        rows.push(['external attempts', String(Math.max(0, Number.isFinite(run.externalAttempts) ? run.externalAttempts : 0))]);
        rows.push(['denied actions', String(Math.max(0, Number.isFinite(run.deniedActions) ? run.deniedActions : 0))]);
        rows.push(['blocked actions', String(Math.max(0, Number.isFinite(run.blockedActions) ? run.blockedActions : 0))]);
        rows.push(['plan required', String(run.planRequired === true)]);
        rows.push(['host boundary preserved', String(run.hostBoundaryPreserved !== false)]);
        rows.push(['lifecycle requested', String(Math.max(0, Number(run.actionLifecycleCounts && run.actionLifecycleCounts.requested) || 0))]);
        rows.push(['lifecycle approval_required', String(Math.max(0, Number(run.actionLifecycleCounts && run.actionLifecycleCounts.approvalRequired) || 0))]);
        rows.push(['lifecycle blocked', String(Math.max(0, Number(run.actionLifecycleCounts && run.actionLifecycleCounts.blocked) || 0))]);
        rows.push(['lifecycle executed', String(Math.max(0, Number(run.actionLifecycleCounts && run.actionLifecycleCounts.executed) || 0))]);
        rows.push(['lifecycle failed', String(Math.max(0, Number(run.actionLifecycleCounts && run.actionLifecycleCounts.failed) || 0))]);
        rows.push(['model used', modelUsed]);
        rows.push(['embeddings', embeddingsSummary]);
        rows.push(['duration', normalizeStatusCell(run.duration, 'not available')]);
        rows.push(['workspace', normalizeStatusCell(run.workspace, 'not available')]);
        rows.push(['task', normalizeStatusCell(run.taskPreview, 'not available')]);

        for (const row of rows) {
          const key = document.createElement('div');
          key.className = 'kv-key';
          key.textContent = row[0];
          const value = document.createElement('div');
          value.className = 'kv-value';
          value.textContent = row[1] || 'not available';
          if (row[0] === 'task') {
            value.classList.add('task-preview');
            value.title = String(row[1] || '').trim();
          }
          runStatusGrid.appendChild(key);
          runStatusGrid.appendChild(value);
        }
      }

      function normalizeStatusCell(value, fallback) {
        const text = String(value === undefined || value === null ? '' : value).trim();
        if (!text || text === 'not available') {
          return fallback || 'not available';
        }
        return text;
      }`;

function getWebviewClientStatusRenderer() {
  return webviewClientStatusRenderer;
}

module.exports = { getWebviewClientStatusRenderer };
