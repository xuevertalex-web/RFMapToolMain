const assert = require('assert');
const { extractStructuredResult } = require('./agentRunner');

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

testMarkedStructuredResult();
testLegacyStructuredResultFallback();
testMalformedMarkedResultFallsBack();
testFullBufferStructuredResultFallback();

console.log('agentRunner tests passed');
