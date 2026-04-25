const webviewClientModelStatusRenderer = `function renderModelStatus(run) {
        if (!modelName || !modelPing) {
          return;
        }

        const provider = normalizeStatusValue(run && run.provider, '');
        const model = normalizeStatusValue(run && run.model, '');
        const duration = normalizeStatusValue(run && run.duration, '');
        const degraded = Array.isArray(run && run.degradedFlags) && run.degradedFlags.length > 0;

        if (model || provider) {
          modelName.textContent = 'Модель: ' + [provider, model].filter(Boolean).join(' / ');
        } else {
          modelName.textContent = 'Модель: не определена';
        }

        if (duration) {
          modelPing.textContent = 'Пинг: ' + duration;
        } else if (degraded) {
          modelPing.textContent = 'Пинг: degraded';
        } else if (run && run.ok) {
          modelPing.textContent = 'Пинг: ok';
        } else if (run && run.failed) {
          modelPing.textContent = 'Пинг: ошибка';
        } else {
          modelPing.textContent = 'Пинг: нет данных';
        }
      }

      function setModelStatusRunning() {
        if (modelPing) {
          modelPing.textContent = 'Пинг: проверка...';
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
