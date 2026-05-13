const assert = require('assert');
const path = require('path');
const os = require('os');
const fs = require('fs');

// Mock vscode to avoid actual UI
const Module = require('module');
const originalRequire = Module.prototype.require;
Module.prototype.require = function patchedRequire(request) {
  if (request === 'vscode') {
    return {
      workspace: { workspaceFolders: [] },
      window: { activeTextEditor: null }
    };
  }
  return originalRequire.apply(this, arguments);
};

const { resolveWorkspaceRoot } = require('./workspaceResolver');
Module.prototype.require = originalRequire;
const { isAnalysisOnlyTask } = require('./workspaceTaskClassifier');

function mkd(name) {
  return fs.mkdtempSync(path.join(os.tmpdir(), name));
}

function assertClassifierAllows(task) {
  assert.strictEqual(isAnalysisOnlyTask(task), true, `Task "${task}" should be classified as analysis-only`);
}

function assertClassifierBlocks(task) {
  assert.strictEqual(isAnalysisOnlyTask(task), false, `Task "${task}" should be classified as explicit mutation`);
}

function runGuardTests() {
  const backendRoot = mkd('lca-backend-');
  const backendProjectPath = path.join(backendRoot, 'LocalCursorAgent.csproj');
  fs.writeFileSync(backendProjectPath, '<Project />', 'utf8');

  const allowExamples = [
    'тут?',
    'тут',
    '`тут?`',
    'here?',
    'here',
    'что тут?',
    'what can you do',
    'explain this project',
    'fix it',
    'make it better',
    'почини',
    'ало',
    'агент'
  ];

  for (const task of allowExamples) {
    assertClassifierAllows(task);
    const result = resolveWorkspaceRoot({
      configuredWorkspaceRoot: backendRoot,
      backendProjectPath,
      allowBackendWorkspace: false,
      analysisOnlyTask: isAnalysisOnlyTask(task)
    });
    assert.ok(result.workspaceRoot, `Task "${task}" should be allowed in backend workspace`);
  }

  const blockExamples = [
    'create file TEST.md',
    'delete TEST.md',
    'remove file TEST.md',
    'rename TEST.md',
    'fix ContextBuilder.cs',
    'update package.json',
    'change this file',
    'modify commandHandlers.js',
    'создай файл TEST.md',
    'удали TEST.md',
    'исправь ContextBuilder.cs',
    'обнови package.json'
  ];

  for (const task of blockExamples) {
    assertClassifierBlocks(task);
    const result = resolveWorkspaceRoot({
      configuredWorkspaceRoot: backendRoot,
      backendProjectPath,
      allowBackendWorkspace: false,
      analysisOnlyTask: isAnalysisOnlyTask(task)
    });
    assert.strictEqual(result.workspaceRoot, '', `Task "${task}" should be blocked in backend workspace`);
    assert.strictEqual(result.reason, 'backend_workspace_blocked');
  }

  console.log('workspace guard tests passed');
}

runGuardTests();
