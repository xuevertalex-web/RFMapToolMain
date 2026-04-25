const webviewClientModelStatusRenderer = `function renderModelStatus(run) {
        if (!modelPing) {
          return;
        }

        const duration = normalizeStatusValue(run && run.duration, '');
        const degraded = Array.isArray(run && run.degradedFlags) && run.degradedFlags.length > 0;

        if (duration) {
          modelPing.textContent = 'Ping: ' + duration;
        } else if (degraded) {
          modelPing.textContent = 'Ping: degraded';
        } else if (run && run.ok) {
          modelPing.textContent = 'Ping: ok';
        } else if (run && run.failed) {
          modelPing.textContent = 'Ping: error';
        } else {
          modelPing.textContent = 'Ping: no data';
        }
      }

      function setModelStatusRunning() {
        if (modelPing) {
          modelPing.textContent = 'Ping: checking...';
        }
      }

      function normalizeStatusValue(value, fallback) {
        const text = String(value === undefined || value === null ? '' : value).trim();
        return text === 'not available' ? fallback : text;
      }`;

function getWebviewClientModelStatusRenderer() {
  return webviewClientModelStatusRenderer;
}

module.exports = { getWebviewClientModelStatusRenderer };
