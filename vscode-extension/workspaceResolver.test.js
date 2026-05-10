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

function mkd(name) {
  return fs.mkdtempSync(path.join(os.tmpdir(), name));
}

function run() {
  const backendRoot = mkd('lca-backend-');
  const backendProjectPath = path.join(backendRoot, 'LocalCursorAgent.csproj');
  fs.writeFileSync(backendProjectPath, '<Project />', 'utf8');

  const analysisOnly = resolveWorkspaceRoot({
    configuredWorkspaceRoot: backendRoot,
    backendProjectPath,
    allowBackendWorkspace: false,
    analysisOnlyTask: true
  });
  assert.ok(analysisOnly.workspaceRoot, 'analysis-only task should not be blocked in backend workspace');
  assert.strictEqual(path.resolve(analysisOnly.workspaceRoot).toLowerCase(), path.resolve(backendRoot).toLowerCase(), 'resolver should return backend workspace root for analysis-only');

  const clarifyLike = resolveWorkspaceRoot({
    configuredWorkspaceRoot: backendRoot,
    backendProjectPath,
    allowBackendWorkspace: false,
    analysisOnlyTask: true
  });
  assert.ok(clarifyLike.workspaceRoot, 'clarify-like non-mutation task should not be blocked in backend workspace');

  const execute = resolveWorkspaceRoot({
    configuredWorkspaceRoot: backendRoot,
    backendProjectPath,
    allowBackendWorkspace: false,
    analysisOnlyTask: false
  });
  assert.strictEqual(execute.workspaceRoot, '', 'execute/mutation task should be blocked in backend workspace');
  assert.strictEqual(execute.reason, 'backend_workspace_blocked');
  assert.ok(String(execute.backendProjectPath || '').toLowerCase().endsWith('localcursoragent.csproj'), 'blocked outcome should include backend project path for clear user guidance');
}

run();
console.log('workspaceResolver tests passed');
