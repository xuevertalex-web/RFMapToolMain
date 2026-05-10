const assert = require('assert');
const { extractStructuredResult, composeTaskWithContinuation, preflightBackendProjectPath } = require('./agentRunner');
const path = require('path');
const { getWebviewClientResultHandlers } = require('./webviewClientResultHandlers');

function testMarkedStructuredResult() {
  const result = extractStructuredResult([
    'RuntimeRoot: C:\\agent',
    '__LOCAL_CURSOR_AGENT_RESULT_START__',
    '{"ok":true,"message":"done","changedFiles":[]}',
    '__LOCAL_CURSOR_AGENT_RESULT_END__',
    'Latest manifest: C:\\agent\\.agent-runtime\\latest_manifest.json'
  ]);

  assert.deepStrictEqual(result, {
    ok: true,
    message: 'done',
    changedFiles: []
  });
}

function testLegacyStructuredResultFallback() {
  const result = extractStructuredResult([
    'RuntimeRoot: C:\\agent',
    '{"ok":false,"message":"failed"}',
    'Latest manifest: C:\\agent\\.agent-runtime\\latest_manifest.json'
  ]);

  assert.deepStrictEqual(result, {
    ok: false,
    message: 'failed'
  });
}

function testMalformedMarkedResultFallsBack() {
  const result = extractStructuredResult([
    '__LOCAL_CURSOR_AGENT_RESULT_START__',
    '{not json}',
    '__LOCAL_CURSOR_AGENT_RESULT_END__',
    '{"ok":true,"message":"fallback"}'
  ]);

  assert.deepStrictEqual(result, {
    ok: true,
    message: 'fallback'
  });
}

function testFullBufferStructuredResultFallback() {
  const stdout = [
    'RuntimeRoot: C:\\agent',
    'Some chunk before JSON {"not":"result"}',
    '{"ok":true,"finalStatus":"success","summary":"done from full buffer","changedFiles":[]}',
    'Latest manifest: C:\\agent\\.agent-runtime\\latest_manifest.json'
  ].join('\n');

  const result = extractStructuredResult([], stdout, '');
  assert.deepStrictEqual(result, {
    ok: true,
    finalStatus: 'success',
    summary: 'done from full buffer',
    changedFiles: []
  });
}

function testComposeTaskWithContinuation() {
  const composed = composeTaskWithContinuation('continue', {
    continuationHint: 'Provide a step-by-step implementation plan and execute the first concrete edit.',
    nextActionCandidates: ['Draft a 3-step implementation plan.', 'Select target file and symbol.'],
    sessionContinuation: {
      lastSuccessfulStep: 'ToolCallsExecuted',
      lastKnownAction: 'Executed 1 tool calls'
    }
  });

  assert.ok(composed.includes('Continue from previous run.'));
  assert.ok(composed.includes('Continuation hint:'));
  assert.ok(composed.includes('Next action candidates:'));
}

function testPreflightBackendProjectPath() {
  const missing = preflightBackendProjectPath('', process.cwd(), process.cwd());
  assert.strictEqual(missing.ok, false);
  assert.strictEqual(missing.code, 'backend_path_empty');

  const notAbsolute = preflightBackendProjectPath('LocalCursorAgent.csproj', process.cwd(), process.cwd());
  assert.strictEqual(notAbsolute.ok, false);
  assert.strictEqual(notAbsolute.code, 'backend_path_not_absolute');

  const absentAbsolute = preflightBackendProjectPath(path.join(process.cwd(), 'missing.csproj'), process.cwd(), process.cwd());
  assert.strictEqual(absentAbsolute.ok, false);
  assert.strictEqual(absentAbsolute.code, 'backend_path_not_found');
}

function testClarificationExamplesWiredInResultHandlers() {
  const src = getWebviewClientResultHandlers();
  assert.ok(src.includes('CLARIFICATION_REQUIRED'));
  assert.ok(src.includes('Нужно уточнить задачу.'));
  assert.ok(src.includes('Пример: Исправь ошибку сборки'));
  assert.ok(src.includes('Пример: Добавь проверку пути backendProjectPath'));
}

testMarkedStructuredResult();
testLegacyStructuredResultFallback();
testMalformedMarkedResultFallsBack();
testFullBufferStructuredResultFallback();
testComposeTaskWithContinuation();
testPreflightBackendProjectPath();
testClarificationExamplesWiredInResultHandlers();

console.log('agentRunner tests passed');
