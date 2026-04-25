const webviewClientSummaryRenderer = `function renderRunSummary(run) {
        result.textContent = run.messageText || 'not available';
        result.classList.toggle('muted', !run.messageText);
        resultBadge.textContent = run.failed ? 'failed' : run.ok ? 'success' : 'running';
        resultBadge.className = 'result-badge ' + (run.failed ? 'error' : run.ok ? 'ok' : 'running');
        summary.textContent = run.summary || 'not available';
        summary.className = 'summary-box ' + (run.failed ? 'error' : run.buildSucceeded === false ? 'warn' : run.ok ? 'ok' : 'na');
      }`;

function getWebviewClientSummaryRenderer() {
  return webviewClientSummaryRenderer;
}

module.exports = { getWebviewClientSummaryRenderer };
