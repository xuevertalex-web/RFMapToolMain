const webviewClientStatusRenderer = `function renderRunStatus(run) {
        if (!runStatusGrid) return;
        runStatusGrid.replaceChildren();
        const rows = [
          ['status', run.status],
          ['final status', run.finalStatus || run.status],
          ['fallback reason', run.fallbackReason || 'not available'],
          ['fallback mode', run.fallbackMode || 'not available'],
          ['workspace', run.workspace],
          ['task', run.taskPreview],
          ['duration', run.duration],
          ['provider', run.provider],
          ['model', run.model],
          ['build', run.buildText],
          ['degraded', run.degradedFlags.length ? run.degradedFlags.join(', ') : 'not available']
        ];
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
      }`;

function getWebviewClientStatusRenderer() {
  return webviewClientStatusRenderer;
}

module.exports = { getWebviewClientStatusRenderer };
