const webviewClientModelSelector = `function updateModelSelectorUi() {
        if (!modelSelector) {
          return;
        }

        const previousValue = modelSelector.value;
        modelSelector.replaceChildren();
        const models = Array.isArray(availableOllamaModels) ? availableOllamaModels : [];

        if (models.length === 0) {
          const placeholder = document.createElement('option');
          placeholder.value = '';
          placeholder.textContent = 'No models';
          modelSelector.appendChild(placeholder);
          modelSelector.disabled = true;
          return;
        }

        const selected = selectedOllamaModel || defaultOllamaModel || models[0];
        for (const model of models) {
          const option = document.createElement('option');
          option.value = model;
          option.textContent = model;
          modelSelector.appendChild(option);
        }

        modelSelector.value = models.includes(selected) ? selected : (models.includes(previousValue) ? previousValue : models[0]);
        selectedOllamaModel = modelSelector.value;
        modelSelector.disabled = uiRunning;
      }

      function renderModelSelectionStatusLine() {
        if (!modelSelectionStatus) {
          return;
        }
        modelSelectionStatus.textContent = '';
        modelSelectionStatus.style.display = 'none';
      }

      function setModelSelectionStatusWarning(text, tone) {
        const message = String(text || '').trim();
        if (!message) {
          return;
        }

        showModelSelectionToast(message, tone === 'warn' ? 'warn' : '');
      }

      function showModelSelectionToast(text, tone) {
        if (!modelSelectionToast) {
          return;
        }

        const message = String(text || '').trim();
        if (!message) {
          return;
        }

        modelSelectionToast.textContent = message;
        modelSelectionToast.classList.add('show');
        modelSelectionToast.classList.toggle('warn', tone === 'warn');
        if (modelSelectionToastTimer) {
          window.clearTimeout(modelSelectionToastTimer);
        }
        modelSelectionToastTimer = window.setTimeout(() => {
          modelSelectionToast.classList.remove('show');
          modelSelectionToast.classList.remove('warn');
          modelSelectionToast.textContent = '';
          modelSelectionToastTimer = null;
        }, 1000);
      }

      function applyModelSelectionState(payload) {
        const state = payload && typeof payload === 'object' ? payload : {};
        availableOllamaModels = Array.isArray(state.availableModels)
          ? state.availableModels.map(item => String(item || '').trim()).filter(Boolean)
          : [];
        defaultOllamaModel = String(state.defaultModel || '').trim();
        selectedOllamaModel = String(state.selectedModel || '').trim();
        selectedOllamaModelSource = String(state.source || '').trim();
        const pingText = String(state.pingText || '').trim();
        const pingMessage = String(state.pingMessage || '').trim();
        const notice = String(state.notice || '').trim();

        if (modelPing) {
          modelPing.textContent = pingText
            ? ('Ping: ' + pingText)
            : 'Ping: no data';
          modelPing.title = pingMessage || '';
        }

        const warning = String(state.warning || '').trim();
        updateModelSelectorUi();
        renderModelSelectionStatusLine();
        setModelSelectionStatusWarning(warning, warning ? 'warn' : '');
        if (notice) {
          showModelSelectionToast(notice, '');
        }
        saveWebviewState();
      }

      function requestModelSelectionState() {
        vscode.postMessage({ type: 'getModelSelectionState' });
      }

      function handleModelSelectorChange() {
        if (!modelSelector) {
          return;
        }

        const model = String(modelSelector.value || '').trim();
        selectedOllamaModel = model;
        selectedOllamaModelSource = 'saved';
        renderModelSelectionStatusLine();
        if (modelPing) {
          modelPing.textContent = 'Ping: checking...';
          modelPing.title = '';
        }
        saveWebviewState();
        vscode.postMessage({ type: 'setSelectedModel', model });
      }`;

function getWebviewClientModelSelector() {
  return webviewClientModelSelector;
}

module.exports = { getWebviewClientModelSelector };
