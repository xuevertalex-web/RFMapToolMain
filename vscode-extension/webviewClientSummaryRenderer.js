const webviewClientSummaryRenderer = `function renderRunSummary(run) {
        result.textContent = run.messageText || 'not available';
        result.classList.toggle('muted', !run.messageText);
        const badgeStatus = run.status || (run.failed ? 'error' : run.ok ? 'success' : 'running');
        resultBadge.textContent = badgeStatus;
        resultBadge.className = 'result-badge ' + (badgeStatus === 'error' ? 'error' : badgeStatus === 'running' ? 'running' : 'ok');
        summary.textContent = run.summary || 'not available';
        summary.className = 'summary-box ' + (badgeStatus === 'error' ? 'error' : run.buildSucceeded === false ? 'warn' : badgeStatus === 'running' ? 'na' : 'ok');
      }`;

function getWebviewClientSummaryRenderer() {
  return webviewClientSummaryRenderer;
}

module.exports = { getWebviewClientSummaryRenderer };
