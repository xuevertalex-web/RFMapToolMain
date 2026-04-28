const webviewClientSummaryRenderer = `function renderRunSummary(run) {
        result.textContent = run.messageText || 'not available';
        result.classList.toggle('muted', !run.messageText);
        const badgeStatus = run.status || (run.failed ? 'error' : run.ok ? 'success' : 'running');
        resultBadge.textContent = badgeStatus;
        const isFallbackSuccess = badgeStatus === 'fallback-success';
        const buildFailed = run.buildStarted === true && run.buildSucceeded === false;
        const buildNotStarted = run.buildStarted === false || String(run.buildText || '').trim().toLowerCase() === 'not started';
        resultBadge.className = 'result-badge ' + (badgeStatus === 'error' ? 'error' : (badgeStatus === 'running' || isFallbackSuccess) ? 'running' : 'ok');
        const baseSummary = run.summary || 'not available';
        const nextActions = Array.isArray(run.nextActionCandidates) ? run.nextActionCandidates.filter(Boolean).slice(0, 3) : [];
        summary.textContent = nextActions.length ? (baseSummary + '\\nNext actions: ' + nextActions.join(' | ')) : baseSummary;
        summary.className = 'summary-box ' + (
          badgeStatus === 'error'
            ? 'error'
            : (badgeStatus === 'running' || buildNotStarted)
              ? 'na'
              : (isFallbackSuccess || buildFailed || run.embeddingsWarning)
                ? 'warn'
                : 'ok'
        );
      }`;

function getWebviewClientSummaryRenderer() {
  return webviewClientSummaryRenderer;
}

module.exports = { getWebviewClientSummaryRenderer };
