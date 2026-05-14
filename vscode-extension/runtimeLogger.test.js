const assert = require('assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { createRuntimeLogger } = require('./runtimeLogger');

function mkd(prefix) {
  return fs.mkdtempSync(path.join(os.tmpdir(), prefix));
}

function readJsonlLines(filePath) {
  return fs.readFileSync(filePath, 'utf8').trim().split('\n').filter(Boolean).map(line => JSON.parse(line));
}

function run() {
  const extensionRoot = mkd('lca-ext-');
  const backendRoot = mkd('lca-back-');
  const backendProjectPath = path.join(backendRoot, 'LocalCursorAgent.csproj');
  fs.writeFileSync(backendProjectPath, '<Project />', 'utf8');

  const logger = createRuntimeLogger({
    extensionRoot,
    workspaceRoot: path.join(mkd('lca-missing-'), 'does-not-exist'),
    backendProjectPath
  });
  logger.info('workspace-guard precheck', {
    source: 'command',
    task: 'SECRET_TOKEN=abc123 do not leak',
    taskCategory: 'mutation',
    taskLength: 30,
    taskHash: 'deadbeef1234',
    targetWorkspacePath: 'C:\\very\\sensitive\\path'
  });

  const runtimeDir = path.join(backendRoot, '.agent-runtime');
  assert.ok(fs.existsSync(runtimeDir), 'logger should fallback to validated backend root');
  const jsonlPath = path.join(runtimeDir, 'agent.jsonl');
  assert.ok(fs.existsSync(jsonlPath), 'jsonl log should be written');

  const entries = readJsonlLines(jsonlPath);
  const entry = entries[entries.length - 1];
  const payload = JSON.stringify(entry);
  assert.ok(!payload.includes('SECRET_TOKEN=abc123'), 'raw task text must not be persisted');
  assert.ok(!payload.includes('C:\\\\very\\\\sensitive\\\\path'), 'raw workspace path must not be persisted');
  assert.strictEqual(entry.meta.taskCategory, 'mutation');
  assert.strictEqual(entry.meta.taskHash, 'deadbeef1234');
  assert.strictEqual(entry.meta.targetWorkspacePath, '[redacted]');
}

function runRotation() {
  const extensionRoot = mkd('lca-ext-');
  const workspaceRoot = mkd('lca-ws-');
  const logger = createRuntimeLogger({
    extensionRoot,
    workspaceRoot,
    maxFileSizeBytes: 180,
    maxGenerations: 3
  });

  for (let i = 0; i < 40; i++) {
    logger.info(`event-${i}-` + 'x'.repeat(80), {
      source: 'command',
      taskCategory: 'analysis_or_chat',
      taskLength: 10,
      taskHash: `h${i}`
    });
  }

  const runtimeDir = path.join(workspaceRoot, '.agent-runtime');
  const textLogPath = path.join(runtimeDir, 'agent.log');
  const jsonlLogPath = path.join(runtimeDir, 'agent.jsonl');
  assert.ok(fs.existsSync(textLogPath), 'text log should exist after rotation');
  assert.ok(fs.existsSync(jsonlLogPath), 'jsonl log should exist after rotation');
  assert.ok(fs.existsSync(`${textLogPath}.1`), 'text log rotation .1 expected');
  assert.ok(fs.existsSync(`${jsonlLogPath}.1`), 'jsonl log rotation .1 expected');

  const textRotated = [1, 2, 3].filter(n => fs.existsSync(`${textLogPath}.${n}`));
  const jsonRotated = [1, 2, 3].filter(n => fs.existsSync(`${jsonlLogPath}.${n}`));
  assert.ok(textRotated.length <= 3, 'text log generations must be capped');
  assert.ok(jsonRotated.length <= 3, 'jsonl log generations must be capped');
  assert.ok(!fs.existsSync(`${textLogPath}.4`), 'text log .4 must not exist');
  assert.ok(!fs.existsSync(`${jsonlLogPath}.4`), 'jsonl log .4 must not exist');

  logger.warn('post-rotation-write', {
    source: 'command',
    task: 'DO_NOT_LEAK_TOKEN_123',
    taskCategory: 'mutation',
    taskLength: 22,
    taskHash: 'cafebabefeed',
    targetWorkspacePath: 'C:\\secret\\dir'
  });
  const latest = readJsonlLines(jsonlLogPath).pop();
  const payload = JSON.stringify(latest);
  assert.ok(!payload.includes('DO_NOT_LEAK_TOKEN_123'), 'redaction must hold after rotation');
  assert.ok(!payload.includes('C:\\\\secret\\\\dir'), 'path redaction must hold after rotation');
}

function runErrorSanitizationAndFailureSignal() {
  const extensionRoot = mkd('lca-ext-');
  const workspaceRoot = mkd('lca-ws-');
  let failureSignals = 0;
  const logger = createRuntimeLogger({
    extensionRoot,
    workspaceRoot,
    onLoggerFailure: () => { failureSignals++; }
  });

  const noisyError = new Error('X'.repeat(500));
  noisyError.name = 'ActivationFailure';
  noisyError.stack = `ActivationFailure: boom\n    at C:\\secret\\file.js:10`;
  logger.error('activation failed', { error: noisyError });

  const jsonlPath = path.join(workspaceRoot, '.agent-runtime', 'agent.jsonl');
  const entry = readJsonlLines(jsonlPath).pop();
  assert.strictEqual(entry.meta.error.name, 'ActivationFailure');
  assert.ok(entry.meta.error.message.length <= 240, 'sanitized error message should be compact');
  const payload = JSON.stringify(entry);
  assert.ok(!payload.includes('at C:\\\\secret\\\\file.js:10'), 'full stack must not be persisted');

  const originalMkdirSync = fs.mkdirSync;
  fs.mkdirSync = () => { throw new Error('mkdir blocked for test'); };
  try {
    logger.info('first fail');
    logger.info('second fail');
  } finally {
    fs.mkdirSync = originalMkdirSync;
  }
  assert.strictEqual(failureSignals, 1, 'logger failure callback should fire once');
}

run();
runRotation();
runErrorSanitizationAndFailureSignal();
console.log('runtimeLogger tests passed');
