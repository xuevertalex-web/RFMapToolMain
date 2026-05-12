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

function mkd(name) {
  return fs.mkdtempSync(path.join(os.tmpdir(), name));
}

function runGuardTests() {
  const backendRoot = mkd('lca-backend-');
  const backendProjectPath = path.join(backendRoot, 'LocalCursorAgent.csproj');
  fs.writeFileSync(backendProjectPath, '<Project />', 'utf8');

  const allowExamples = [
    'тут',
    'что ты умеешь',
    'объясни проект',
    'ало',
    'агент',
    'почини'
  ];

  for (const task of allowExamples) {
    const result = resolveWorkspaceRoot({
      configuredWorkspaceRoot: backendRoot,
      backendProjectPath,
      allowBackendWorkspace: false,
      analysisOnlyTask: true // simulate low‑signal/analysis; actual detection done in UI before call
    });
    assert.ok(result.workspaceRoot, `Task "${task}" should be allowed (analysis‑only)`);
  }

  const blockExamples = [
    'создай файл TEST.md',
    'удали TEST.md',
    'исправь ContextBuilder.cs'
  ];

  for (const task of blockExamples) {
    const result = resolveWorkspaceRoot({
      configuredWorkspaceRoot: backendRoot,
      backendProjectPath,
      allowBackendWorkspace: false,
      analysisOnlyTask: false // mutation detected by UI
    });
    assert.strictEqual(result.workspaceRoot, '', `Task "${task}" should be blocked`);
    assert.strictEqual(result.reason, 'backend_workspace_blocked');
  }

  console.log('workspace guard tests passed');
}

runGuardTests();
