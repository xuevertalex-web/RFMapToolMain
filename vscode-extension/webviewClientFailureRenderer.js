const webviewClientFailureRenderer = `function renderFailureSummary(run) {
        if (!failureSection || !failureSummary) return;
        failureSummary.replaceChildren();
        if (!run.failed || !run.failure) {
          failureSection.style.display = 'none';
          return;
        }
        failureSection.style.display = 'block';
        const rows = [
          ['root cause code', run.failure.rootCauseCode],
          ['failed stage', run.failure.failedStage],
          ['last successful step', run.failure.lastSuccessfulStep],
          ['failed step', run.failure.failedStep],
          ['reason code', run.failure.reasonCode],
          ['explanation', run.failure.explanation],
          ['why pipeline stopped', run.failure.pipelineStoppedReason],
          ['downstream not started', run.failure.downstreamNotStarted],
          ['loop stage', run.failure.loopStage],
          ['iterations', run.failure.iterations],
          ['last known action', run.failure.lastKnownAction],
          ['model call started', run.failure.modelCallStarted],
          ['patch started', run.failure.patchStarted],
          ['build started', run.failure.buildStarted]
        ];
        for (const row of rows) {
          const key = document.createElement('div');
          key.className = 'kv-key';
          key.textContent = row[0];
          const value = document.createElement('div');
          value.className = 'kv-value';
          value.textContent = row[1] || 'not available';
          failureSummary.appendChild(key);
          failureSummary.appendChild(value);
        }
      }`;

function getWebviewClientFailureRenderer() {
  return webviewClientFailureRenderer;
}

module.exports = { getWebviewClientFailureRenderer };
