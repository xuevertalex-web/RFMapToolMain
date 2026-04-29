const assert = require('assert');
const vm = require('vm');
const { getWebviewBody } = require('./webviewBody');
const { getWebviewClientModelSelector } = require('./webviewClientModelSelector');
const { getWebviewClientRunNormalization } = require('./webviewClientRunNormalization');
const { getWebviewClientStatusRenderer } = require('./webviewClientStatusRenderer');
const { getWebviewClientSummaryRenderer } = require('./webviewClientSummaryRenderer');
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

function createGridElement() {
  return {
    children: [],
    replaceChildren() {
      this.children = [];
    },
    appendChild(child) {
      this.children.push(child);
    }
  };
}

function createTextBlock() {
  return {
    textContent: '',
    className: '',
    classList: createClassList()
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
    runStatusGrid: createGridElement(),
    result: createTextBlock(),
    resultBadge: createTextBlock(),
    summary: createTextBlock(),
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
  vm.runInNewContext(getWebviewClientStatusRenderer(), context, { filename: 'webviewClientStatusRenderer.js' });
  vm.runInNewContext(getWebviewClientSummaryRenderer(), context, { filename: 'webviewClientSummaryRenderer.js' });
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
  assert.strictEqual(success.reasonCode, '');
  assert.strictEqual(success.modelUsed, 'ollama / qwen2.5-coder:7b');
  assert.strictEqual(success.changedFilesCount, 0);
  assert.strictEqual(success.approvalRequiredCount, 0);
  assert.strictEqual(success.externalAttempts, 0);
  assert.strictEqual(success.deniedActions, 0);
  assert.strictEqual(success.blockedActions, 0);
  assert.strictEqual(success.approvalStatusSummary.allowed, 0);
  assert.strictEqual(success.approvalStatusSummary.approvalRequired, 0);
  assert.strictEqual(success.approvalStatusSummary.denied, 0);
  assert.strictEqual(success.approvalStatusSummary.notApplicable, 0);
  assert.strictEqual(success.planRequired, false);
  assert.strictEqual(success.continuationHint, '');
  assert.strictEqual(success.sessionContinuation.lastSuccessfulStep, '');
  assert.strictEqual(success.sessionContinuation.lastKnownAction, '');
  assert.strictEqual(Array.isArray(success.nextActionCandidates), true);
  assert.strictEqual(success.nextActionCandidates.length, 0);
  assert.strictEqual(success.hostBoundaryPreserved, true);
  assert.strictEqual(success.buildText, 'not started');
  assert.strictEqual(success.embeddingsSummary, 'not available');
  assert.strictEqual(success.embeddingsWarning, false);
  assert.strictEqual(success.runtimeProfile, 'not available');
  assert.strictEqual(success.runtimeEndpoint, 'not available');
  assert.strictEqual(success.configuredContextWindow, 'not available');
  assert.strictEqual(success.configuredGpuOffloadOptions, 'not available');
  assert.strictEqual(success.gpuUsageMeasured, false);
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
  assert.match(timeoutFallback.messageText, /timed out/i);
  assertNoMojibake(timeoutFallback.messageText, 'timeout fallback message');

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
  assert.match(requestFailureFallback.messageText, /request failed/i);
  assertNoMojibake(requestFailureFallback.messageText, 'request fallback message');

  const embeddingsDisabledNotFound = context.normalizeRunResult({
    ok: true,
    structuredResult: {
      ok: true,
      finalStatus: 'success',
      message: 'ok',
      embeddingsStatus: 'NotFound',
      degradedFlags: ['embeddings']
    }
  });
  assert.strictEqual(embeddingsDisabledNotFound.status, 'success');
  assert.strictEqual(embeddingsDisabledNotFound.embeddingsSummary, 'disabled (model not found)');
  assert.strictEqual(embeddingsDisabledNotFound.embeddingsWarning, false);

  const embeddingsDegraded = context.normalizeRunResult({
    ok: true,
    structuredResult: {
      ok: true,
      finalStatus: 'success',
      message: 'ok',
      embeddingsStatus: 'degraded',
      degradedFlags: ['embeddings']
    }
  });
  assert.strictEqual(embeddingsDegraded.status, 'success');
  assert.strictEqual(embeddingsDegraded.embeddingsSummary, 'degraded (semantic retrieval limited)');
  assert.strictEqual(embeddingsDegraded.embeddingsWarning, true);

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

  const approvalRun = context.normalizeRunResult({
    ok: false,
    structuredResult: {
      ok: false,
      finalStatus: 'error',
      message: 'approval required',
      approvalRequiredActions: [
        {
          actionType: 'ReadFile',
          path: 'C:/outside.txt',
          command: '',
          normalizedTarget: 'C:/outside.txt',
          riskLevel: 'high',
          reason: 'Target is outside active workspace',
          approvalStatus: 'ApprovalRequired'
        }
      ],
      externalAttempts: 1,
      deniedActions: 1,
      blockedActions: 0,
      continuationHint: 'Continue from: inspect denied action and apply a safe workspace-local edit',
      sessionContinuation: { lastSuccessfulStep: 'ToolCallsExecuted', lastKnownAction: 'Executed 1 tool calls' },
      nextActionCandidates: ['Select target file', 'Apply one safe edit'],
      hostBoundaryPreserved: true
    }
  });
  assert.strictEqual(approvalRun.approvalRequiredCount, 1);
  assert.strictEqual(approvalRun.externalAttempts, 1);
  assert.strictEqual(approvalRun.outsideBoundaryAttempts, 1);
  assert.strictEqual(approvalRun.highRiskApprovalRequiredActions, 1);
  assert.strictEqual(approvalRun.deniedActions, 1);
  assert.strictEqual(approvalRun.blockedActions, 0);
  assert.ok(String(approvalRun.continuationHint).toLowerCase().includes('continue'));
  assert.strictEqual(approvalRun.sessionContinuation.lastSuccessfulStep, 'ToolCallsExecuted');
  assert.strictEqual(approvalRun.sessionContinuation.lastKnownAction, 'Executed 1 tool calls');
  assert.strictEqual(approvalRun.nextActionCandidates.length, 2);
  assert.strictEqual(approvalRun.hostBoundaryPreserved, true);
  assert.strictEqual(approvalRun.actionLifecycleCounts.requested, 0);
  assert.strictEqual(approvalRun.actionLifecycleCounts.approvalRequired, 0);
  assert.strictEqual(approvalRun.actionLifecycleCounts.blocked, 0);
  assert.strictEqual(approvalRun.actionLifecycleCounts.executed, 0);
  assert.strictEqual(approvalRun.actionLifecycleCounts.failed, 0);
  assert.strictEqual(approvalRun.approvalStatusSummary.allowed, 0);
  assert.strictEqual(approvalRun.approvalStatusSummary.approvalRequired, 0);
  assert.strictEqual(approvalRun.approvalStatusSummary.denied, 0);
  assert.strictEqual(approvalRun.approvalStatusSummary.notApplicable, 0);

  const approvalOnlyRun = context.normalizeRunResult({
    ok: false,
    structuredResult: {
      ok: false,
      finalStatus: 'error',
      approvalRequiredActions: [
        {
          actionType: 'RunCommand',
          command: 'type C:/outside.txt',
          normalizedTarget: 'C:/outside.txt',
          riskLevel: 'high',
          reason: 'Target is outside active workspace',
          approvalStatus: 'ApprovalRequired'
        }
      ]
    }
  });
  assert.strictEqual(approvalOnlyRun.approvalRequiredCount, 1);
  assert.strictEqual(approvalOnlyRun.externalAttempts, 1);
  assert.strictEqual(approvalOnlyRun.deniedActions, 0);
  assert.strictEqual(approvalOnlyRun.blockedActions, 0);

  const lifecycleRun = context.normalizeRunResult({
    ok: true,
    structuredResult: {
      ok: true,
      finalStatus: 'success',
      actionLifecycle: [
        { actionType: 'ReadFile', lifecycleState: 'Requested', actionCorrelationId: 'act-1' },
        { actionType: 'ReadFile', lifecycleState: 'ApprovalRequired', actionCorrelationId: 'act-1' },
        { actionType: 'ReadFile', lifecycleState: 'Blocked', actionCorrelationId: 'act-1' },
        { actionType: 'WriteFile', lifecycleState: 'Executed', actionCorrelationId: 'act-2' },
        { actionType: 'Build', lifecycleState: 'Failed', actionCorrelationId: 'act-3' }
      ]
    }
  });
  assert.strictEqual(lifecycleRun.actionLifecycleCounts.requested, 1);
  assert.strictEqual(lifecycleRun.actionLifecycleCounts.approvalRequired, 1);
  assert.strictEqual(lifecycleRun.actionLifecycleCounts.blocked, 1);
  assert.strictEqual(lifecycleRun.actionLifecycleCounts.executed, 1);
  assert.strictEqual(lifecycleRun.actionLifecycleCounts.failed, 1);
  assert.strictEqual(lifecycleRun.deniedActions, 1);
  assert.strictEqual(lifecycleRun.blockedActions, 1);
  assert.strictEqual(lifecycleRun.actionLifecycle[0].actionCorrelationId, 'act-1');
  assert.strictEqual(lifecycleRun.approvalStatusSummary.allowed, 0);
  assert.strictEqual(lifecycleRun.approvalStatusSummary.approvalRequired, 0);
  assert.strictEqual(lifecycleRun.approvalStatusSummary.denied, 0);
  assert.strictEqual(lifecycleRun.approvalStatusSummary.notApplicable, 5);

  const approvalSummaryRun = context.normalizeRunResult({
    ok: true,
    structuredResult: {
      ok: true,
      finalStatus: 'success',
      approvalStatusSummary: {
        allowed: 2,
        approvalRequired: 3,
        denied: 1,
        notApplicable: 4
      }
    }
  });
  assert.strictEqual(approvalSummaryRun.approvalStatusSummary.allowed, 2);
  assert.strictEqual(approvalSummaryRun.approvalStatusSummary.approvalRequired, 3);
  assert.strictEqual(approvalSummaryRun.approvalStatusSummary.denied, 1);
  assert.strictEqual(approvalSummaryRun.approvalStatusSummary.notApplicable, 4);

  const approvalShapeRun = context.normalizeRunResult({
    ok: false,
    structuredResult: {
      ok: false,
      finalStatus: 'error',
      approvalRequiredActions: [
        {
          actionType: 'ReadFile',
          path: 'C:/outside.txt',
          normalizedTarget: 'C:/outside.txt',
          riskLevel: 'high',
          reason: 'Target is outside active workspace',
          approvalStatus: 'ApprovalRequired',
          hiddenThought: 'must not leak'
        }
      ]
    }
  });
  assert.strictEqual(approvalShapeRun.approvalRequiredActions.length, 1);
  assert.strictEqual(Object.prototype.hasOwnProperty.call(approvalShapeRun.approvalRequiredActions[0], 'hiddenThought'), false);
}

function readStatusRows(grid) {
  const rows = [];
  for (let i = 0; i < grid.children.length; i += 2) {
    const key = grid.children[i];
    const value = grid.children[i + 1];
    if (!key || !value) {
      continue;
    }
    rows.push([String(key.textContent || ''), String(value.textContent || '')]);
  }
  return rows;
}

function testStatusAndSummaryRendering() {
  const context = buildContext();

  const successRun = {
    status: 'success',
    ok: true,
    failed: false,
    reasonCode: '',
    finalStatus: 'success',
    fallbackReason: '',
    fallbackMode: '',
    buildText: 'not started',
    buildStarted: false,
    buildSucceeded: false,
    changedFilesCount: 0,
    modelUsed: 'ollama / qwen2.5-coder:7b',
    runtimeProfile: 'ollama/qwen2.5-coder-instruct-q4_k_m',
    runtimeEndpoint: 'http://localhost:11434',
    configuredContextWindow: '8192',
    configuredGpuOffloadOptions: 'num_gpu=1',
    gpuUsageMeasured: false,
    embeddingsSummary: 'not available',
    embeddingsWarning: false,
    duration: '500 ms',
    workspace: 'workspace',
    taskPreview: 'analysis task',
    messageText: 'done',
    summary: 'analysis complete'
  };

  context.renderRunStatus(successRun);
  const successRows = readStatusRows(context.runStatusGrid);
  assert.ok(successRows.some(([key, value]) => key === 'status' && value === 'success'));
  assert.ok(successRows.some(([key, value]) => key === 'build' && value === 'not started'));
  assert.ok(successRows.some(([key, value]) => key === 'approval required' && value === '0'));
  assert.ok(successRows.some(([key, value]) => key === 'blocked actions' && value === '0'));
  assert.ok(successRows.some(([key, value]) => key === 'outside boundary attempts' && value === '0'));
  assert.ok(successRows.some(([key, value]) => key === 'high-risk approval required' && value === '0'));
  assert.ok(successRows.some(([key, value]) => key === 'plan required' && value === 'false'));
  assert.ok(successRows.some(([key, value]) => key === 'continuation hint' && value === 'not available'));
  assert.ok(successRows.some(([key, value]) => key === 'next actions' && value === 'not available'));
  assert.ok(successRows.some(([key, value]) => key === 'host boundary preserved' && value === 'true'));
  assert.ok(successRows.some(([key, value]) => key === 'lifecycle executed' && value === '0'));
  assert.ok(successRows.some(([key, value]) => key === 'approval status allowed' && value === '0'));
  assert.ok(successRows.some(([key, value]) => key === 'approval status required' && value === '0'));
  assert.ok(successRows.some(([key, value]) => key === 'approval status denied' && value === '0'));
  assert.ok(successRows.some(([key, value]) => key === 'approval status n/a' && value === '0'));
  assert.ok(successRows.some(([key, value]) => key === 'model used' && value === 'ollama / qwen2.5-coder:7b'));
  assert.ok(successRows.some(([key, value]) => key === 'runtime profile' && value === 'ollama/qwen2.5-coder-instruct-q4_k_m'));
  assert.ok(successRows.some(([key, value]) => key === 'runtime endpoint' && value === 'http://localhost:11434'));
  assert.ok(successRows.some(([key, value]) => key === 'configured context window' && value === '8192'));
  assert.ok(successRows.some(([key, value]) => key === 'configured gpu offload' && value === 'num_gpu=1'));
  assert.ok(successRows.some(([key, value]) => key === 'gpu usage measured' && value === 'false'));
  assert.ok(!successRows.some(([key]) => key === 'fallback reason'));

  context.renderRunSummary(successRun);
  assert.strictEqual(context.resultBadge.textContent, 'success');
  assert.ok(context.resultBadge.className.includes('ok'));
  assert.ok(context.summary.className.includes('na'));

  const fallbackRun = {
    ...successRun,
    status: 'fallback-success',
    fallbackReason: 'MODEL_TIMEOUT',
    fallbackMode: 'indexed_context',
    reasonCode: 'ANALYSIS_FALLBACK_USED',
    messageText: 'Local model timed out; indexed-context fallback was used.',
    approvalRequiredCount: 2,
    externalAttempts: 2,
    deniedActions: 2,
    blockedActions: 1,
    planRequired: true,
    continuationHint: 'Provide a step-by-step implementation plan and execute the first concrete edit.',
    nextActionCandidates: ['Draft 3-step plan', 'Pick first file', 'Apply first edit'],
    hostBoundaryPreserved: true,
    actionLifecycleCounts: {
      requested: 3,
      approvalRequired: 2,
      blocked: 1,
      executed: 1,
      failed: 0
    },
    approvalStatusSummary: {
      allowed: 1,
      approvalRequired: 2,
      denied: 2,
      notApplicable: 0
    }
  };

  context.renderRunStatus(fallbackRun);
  const fallbackRows = readStatusRows(context.runStatusGrid);
  assert.ok(fallbackRows.some(([key, value]) => key === 'fallback reason' && value === 'MODEL_TIMEOUT'));
  assert.ok(fallbackRows.some(([key, value]) => key === 'fallback mode' && value === 'indexed_context'));
  assert.ok(fallbackRows.some(([key, value]) => key === 'approval required' && value === '2'));
  assert.ok(fallbackRows.some(([key, value]) => key === 'external attempts' && value === '2'));
  assert.ok(fallbackRows.some(([key, value]) => key === 'denied actions' && value === '2'));
  assert.ok(fallbackRows.some(([key, value]) => key === 'blocked actions' && value === '1'));
  assert.ok(fallbackRows.some(([key, value]) => key === 'plan required' && value === 'true'));
  assert.ok(fallbackRows.some(([key, value]) => key === 'continuation hint' && value.includes('step-by-step implementation plan')));
  assert.ok(fallbackRows.some(([key, value]) => key === 'next actions' && value.includes('Draft 3-step plan')));
  assert.ok(fallbackRows.some(([key, value]) => key === 'lifecycle requested' && value === '3'));
  assert.ok(fallbackRows.some(([key, value]) => key === 'lifecycle approval_required' && value === '2'));
  assert.ok(fallbackRows.some(([key, value]) => key === 'lifecycle blocked' && value === '1'));
  assert.ok(fallbackRows.some(([key, value]) => key === 'approval status allowed' && value === '1'));
  assert.ok(fallbackRows.some(([key, value]) => key === 'approval status required' && value === '2'));
  assert.ok(fallbackRows.some(([key, value]) => key === 'approval status denied' && value === '2'));

  context.renderRunSummary(fallbackRun);
  assert.strictEqual(context.resultBadge.textContent, 'fallback-success');
  assert.ok(context.resultBadge.className.includes('running'));
  assert.ok(context.summary.className.includes('na'));
  assert.ok(String(context.summary.textContent || '').includes('Next actions:'), 'Expected summary to include next actions block.');

}

testSelectorAndStatusPresence();
testModelStateHydrationAndSelectorOptions();
testRunNormalizationContracts();
testStatusAndSummaryRendering();

console.log('webview rendering tests passed');
