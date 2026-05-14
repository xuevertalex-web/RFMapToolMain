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

run();
console.log('runtimeLogger tests passed');
