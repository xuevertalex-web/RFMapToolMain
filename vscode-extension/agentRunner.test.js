const assert = require('assert');
const { extractStructuredResult, composeTaskWithContinuation } = require('./agentRunner');

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
  const composed = composeTaskWithContinuation('давай дальше', {
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

testMarkedStructuredResult();
testLegacyStructuredResultFallback();
testMalformedMarkedResultFallsBack();
testFullBufferStructuredResultFallback();
testComposeTaskWithContinuation();

console.log('agentRunner tests passed');
