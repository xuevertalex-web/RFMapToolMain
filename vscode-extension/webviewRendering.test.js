const assert = require('assert');
const vm = require('vm');
const { getWebviewBody } = require('./webviewBody');
const { getWebviewClientModelSelector } = require('./webviewClientModelSelector');
const { getWebviewClientRunNormalization } = require('./webviewClientRunNormalization');
const { buildModelSelectionState } = require('./modelSelection');

function createClassList() {
  const classes = new Set();
  return {
    add(value) {
      classes.add(String(value || ''));
    },
    remove(value) {
      classes.delete(String(value || ''));
    },
    toggle(value, force) {
      const key = String(value || '');
      if (force === undefined) {
        if (classes.has(key)) {
          classes.delete(key);
          return false;
        }
        classes.add(key);
        return true;
      }
      if (force) {
        classes.add(key);
      } else {
        classes.delete(key);
      }
      return !!force;
    }
  };
}

function createOptionElement() {
  return {
    value: '',
    textContent: '',
    classList: createClassList()
  };
}

function createSelectElement() {
  return {
    value: '',
    disabled: false,
    options: [],
    classList: createClassList(),
    replaceChildren() {
      this.options = [];
      this.value = '';
    },
    appendChild(option) {
      this.options.push(option);
      if (!this.value) {
        this.value = option.value;
      }
    }
  };
}

function buildContext() {
  const context = {
    modelSelector: createSelectElement(),
    modelPing: { textContent: '', title: '' },
    modelSelectionStatus: { textContent: '', style: { display: '' } },
    modelSelectionToast: { textContent: '', classList: createClassList() },
    modelSelectionToastTimer: null,
    availableOllamaModels: [],
    defaultOllamaModel: '',
    selectedOllamaModel: '',
    selectedOllamaModelSource: '',
    uiRunning: false,
    suppressPlainResultLog: false,
    currentRunTask: '',
    taskInput: { value: '' },
    normalizeChangedFileEntry: value => {
      if (value && typeof value === 'object') {
        return {
          path: String(value.path || value.file || ''),
          status: String(value.status || 'modified')
        };
      }
      return { path: String(value || ''), status: 'modified' };
    },
    saveWebviewState: () => {},
    vscode: { postMessage: () => {} },
    window: {
      setTimeout: fn => {
        if (typeof fn === 'function') fn();
        return 1;
      },
      clearTimeout: () => {}
    },
    document: {
      createElement(tagName) {
        if (String(tagName || '').toLowerCase() === 'option') {
          return createOptionElement();
        }
        return {
          className: '',
          textContent: '',
          classList: createClassList()
        };
      }
    }
  };

  vm.runInNewContext(getWebviewClientModelSelector(), context, { filename: 'webviewClientModelSelector.js' });
  vm.runInNewContext(getWebviewClientRunNormalization(), context, { filename: 'webviewClientRunNormalization.js' });
  return context;
}

function assertNoMojibake(text, label) {
  const value = String(text || '');
  const hasMojibake = /(Р.|С.)/.test(value);
  assert.strictEqual(hasMojibake, false, `${label} contains mojibake-like text: ${value}`);
}

function testSelectorAndStatusPresence() {
  const body = getWebviewBody();
  assert.ok(body.includes('id="modelSelector"'), 'model selector block is missing');
  assert.ok(body.includes('>Model<'), '"Model" label is missing');
  assert.ok(body.includes('id="status"'), 'status line block is missing');
  assertNoMojibake('Model', 'model label');
  assertNoMojibake('Status', 'status label');
}

function testModelStateHydrationAndSelectorOptions() {
  const context = buildContext();
  const state = buildModelSelectionState('qwen2.5-coder:7b-instruct-q4_K_M', '', 'saved');
  context.applyModelSelectionState({
    ...state,
    pingText: '42 ms',
    pingMessage: 'ok'
  });

  const optionValues = context.modelSelector.options.map(item => item.value);
  assert.deepStrictEqual(optionValues, [
    'qwen2.5-coder:7b',
    'qwen2.5-coder:7b-instruct-q4_K_M'
  ]);
  assert.strictEqual(context.selectedOllamaModel, 'qwen2.5-coder:7b-instruct-q4_K_M');
  assert.strictEqual(context.selectedOllamaModelSource, 'saved');
  assert.strictEqual(context.modelPing.textContent, 'Ping: 42 ms');

  context.applyModelSelectionState({
    ...buildModelSelectionState('', '', 'default'),
    source: 'default',
    pingText: 'offline',
    pingMessage: 'unavailable'
  });
  assert.strictEqual(context.selectedOllamaModelSource, 'default');

  context.applyModelSelectionState({
    ...buildModelSelectionState('unknown-model', 'fallback warning', 'fallback'),
    source: 'fallback',
    pingText: 'offline',
    pingMessage: 'model_not_found'
  });
  assert.strictEqual(context.selectedOllamaModelSource, 'fallback');
}

function testRunNormalizationContracts() {
  const context = buildContext();
  context.currentRunTask = 'analysis task';

  const success = context.normalizeRunResult({
    ok: true,
    structuredResult: {
      ok: true,
      finalStatus: 'success',
      summary: 'analysis complete',
      message: 'done',
      model: 'qwen2.5-coder:7b',
      provider: 'ollama',
      buildStarted: false,
      buildSucceeded: false,
      changedFiles: []
    }
  });
  assert.strictEqual(success.status, 'success');
  assert.strictEqual(success.fallbackReason, '');
  assert.strictEqual(success.fallbackMode, '');
  assert.strictEqual(success.buildText, 'not started');
  assertNoMojibake(success.summary, 'success summary');

  const timeoutFallback = context.normalizeRunResult({
    ok: true,
    structuredResult: {
      ok: true,
      finalStatus: 'fallback-success',
      fallbackReason: 'MODEL_TIMEOUT',
      fallbackMode: 'indexed_context',
      message: 'fallback done',
      buildStarted: false,
      buildSucceeded: false,
      changedFiles: []
    }
  });
  assert.strictEqual(timeoutFallback.status, 'fallback-success');
  assert.strictEqual(timeoutFallback.fallbackReason, 'MODEL_TIMEOUT');

  const requestFailureFallback = context.normalizeRunResult({
    ok: true,
    structuredResult: {
      ok: true,
      finalStatus: 'fallback-success',
      fallbackReason: 'LLM_REQUEST_FAILED',
      fallbackMode: 'indexed_context',
      message: 'fallback done',
      buildStarted: false,
      buildSucceeded: false,
      changedFiles: []
    }
  });
  assert.strictEqual(requestFailureFallback.status, 'fallback-success');
  assert.strictEqual(requestFailureFallback.fallbackReason, 'LLM_REQUEST_FAILED');

  const failure = context.normalizeRunResult({
    ok: false,
    error: 'failed',
    structuredResult: {
      ok: false,
      finalStatus: 'error',
      reasonCode: 'agent_process_failed',
      message: 'failed'
    }
  });
  assert.strictEqual(failure.status, 'error');
  assert.ok(failure.failure, 'failure block should be populated');
}

testSelectorAndStatusPresence();
testModelStateHydrationAndSelectorOptions();
testRunNormalizationContracts();

console.log('webview rendering tests passed');
