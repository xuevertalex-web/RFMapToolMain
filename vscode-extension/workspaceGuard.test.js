const assert = require('assert');
const path = require('path');
const os = require('os');
const fs = require('fs');

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
    '\u0442\u0443\u0442?',
    '\u0442\u0443\u0442',
    '`\u0442\u0443\u0442?`',
    'here?',
    'here',
    '\u0447\u0442\u043e \u0442\u0443\u0442?',
    '\u043e\u043f\u0438\u0448\u0438 \u043f\u0440\u043e\u0435\u043a\u0442',
    '\u0432\u0432\u0443\u0438\u0433\u043f',
    'debug',
    'what can you do',
    'explain this project',
    'fix it',
    'make it better',
    '\u043f\u043e\u0447\u0438\u043d\u0438',
    '\u0430\u043b\u043e',
    '\u0430\u0433\u0435\u043d\u0442'
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
    '\u0441\u043e\u0437\u0434\u0430\u0439 \u0444\u0430\u0439\u043b TEST.md',
    '\u0443\u0434\u0430\u043b\u0438 \u0444\u0430\u0439\u043b TEST.md',
    '\u0438\u0441\u043f\u0440\u0430\u0432\u044c ContextBuilder.cs',
    '\u0438\u0437\u043c\u0435\u043d\u0438 package.json'
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
