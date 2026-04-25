const http = require('http');

const SELECTED_MODEL_STATE_KEY = 'localCursorAgent.selectedOllamaModel';

const AVAILABLE_OLLAMA_MODELS = Object.freeze([
  'qwen2.5-coder:7b',
  'qwen2.5-coder:7b-instruct-q4_K_M'
]);

function getAvailableOllamaModels() {
  return AVAILABLE_OLLAMA_MODELS.slice();
}

function getDefaultOllamaModel() {
  return AVAILABLE_OLLAMA_MODELS[0];
}

function resolveSelectedOllamaModel(candidate) {
  const normalized = String(candidate || '').trim();
  if (normalized && AVAILABLE_OLLAMA_MODELS.includes(normalized)) {
    return { model: normalized, warning: '', source: 'saved' };
  }

  if (normalized) {
    return {
      model: getDefaultOllamaModel(),
      warning: `Saved model "${normalized}" is not available. Falling back to default "${getDefaultOllamaModel()}".`,
      source: 'fallback'
    };
  }

  return { model: getDefaultOllamaModel(), warning: '', source: 'default' };
}

async function loadSelectedOllamaModel(globalState) {
  const stored = globalState.get(SELECTED_MODEL_STATE_KEY, '');
  const resolved = resolveSelectedOllamaModel(stored);
  if (resolved.warning) {
    await globalState.update(SELECTED_MODEL_STATE_KEY, resolved.model);
  }
  return resolved;
}

function getSelectedOllamaModelSync(globalState) {
  const stored = globalState.get(SELECTED_MODEL_STATE_KEY, '');
  return resolveSelectedOllamaModel(stored);
}

async function saveSelectedOllamaModel(globalState, candidate) {
  const resolved = resolveSelectedOllamaModel(candidate);
  await globalState.update(SELECTED_MODEL_STATE_KEY, resolved.model);
  return resolved;
}

function buildModelSelectionState(selectedModel, warning, source) {
  const resolved = resolveSelectedOllamaModel(selectedModel);
  const normalizedSource = String(source || '').trim();
  return {
    availableModels: getAvailableOllamaModels(),
    defaultModel: getDefaultOllamaModel(),
    selectedModel: resolved.model,
    source: normalizedSource || resolved.source,
    warning: String(warning || resolved.warning || '')
  };
}

function normalizeModelName(value) {
  return String(value || '').trim().toLowerCase();
}

function modelNameMatches(left, right) {
  const a = normalizeModelName(left);
  const b = normalizeModelName(right);
  if (!a || !b) {
    return false;
  }

  if (a === b) {
    return true;
  }

  if (a.endsWith(':latest') && a.slice(0, -7) === b) {
    return true;
  }

  if (b.endsWith(':latest') && b.slice(0, -7) === a) {
    return true;
  }

  return false;
}

function requestJson(url, timeoutMs) {
  return new Promise((resolve, reject) => {
    const req = http.get(url, { timeout: timeoutMs }, res => {
      let body = '';
      res.setEncoding('utf8');
      res.on('data', chunk => {
        body += chunk;
      });
      res.on('end', () => {
        const statusCode = Number(res.statusCode || 0);
        if (statusCode < 200 || statusCode >= 300) {
          reject(new Error(`HTTP ${statusCode}`));
          return;
        }
        try {
          resolve(JSON.parse(body));
        } catch (err) {
          reject(err);
        }
      });
    });

    req.on('timeout', () => {
      req.destroy(new Error('timeout'));
    });
    req.on('error', reject);
  });
}

async function probeSelectedModelPing(selectedModel) {
  const startedAt = Date.now();
  try {
    const response = await requestJson('http://127.0.0.1:11434/api/tags', 2500);
    const elapsedMs = Math.max(1, Date.now() - startedAt);
    const models = Array.isArray(response && response.models) ? response.models : [];
    const found = models.some(item => modelNameMatches(item && item.name, selectedModel));
    if (found) {
      return { pingText: `${elapsedMs} ms`, pingOk: true, pingMessage: 'ok' };
    }
    return { pingText: `${elapsedMs} ms (model not found)`, pingOk: false, pingMessage: 'model_not_found' };
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return { pingText: 'offline', pingOk: false, pingMessage: message || 'unavailable' };
  }
}

async function buildModelSelectionStateWithPing(selectedModel, warning, source) {
  const state = buildModelSelectionState(selectedModel, warning, source);
  const ping = await probeSelectedModelPing(state.selectedModel);
  return {
    ...state,
    pingText: ping.pingText,
    pingOk: ping.pingOk,
    pingMessage: ping.pingMessage
  };
}

module.exports = {
  getAvailableOllamaModels,
  getDefaultOllamaModel,
  resolveSelectedOllamaModel,
  loadSelectedOllamaModel,
  getSelectedOllamaModelSync,
  saveSelectedOllamaModel,
  buildModelSelectionState,
  buildModelSelectionStateWithPing
};
