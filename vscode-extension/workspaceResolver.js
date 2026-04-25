const vscode = require('vscode');
const fs = require('fs');
const path = require('path');

function resolveWorkspaceRoot(options = {}) {
  const configuredWorkspaceRoot = String(options.configuredWorkspaceRoot || '').trim();
  const backendProjectPath = String(options.backendProjectPath || '').trim();
  const allowBackendWorkspace = options.allowBackendWorkspace === true;

  if (configuredWorkspaceRoot) {
    if (!fs.existsSync(configuredWorkspaceRoot) || !fs.statSync(configuredWorkspaceRoot).isDirectory()) {
      return {
        workspaceRoot: '',
        reason: 'configured_not_found',
        configuredWorkspaceRoot
      };
    }

    return enforceBackendWorkspaceGuard(
      { workspaceRoot: configuredWorkspaceRoot, reason: 'configured' },
      backendProjectPath,
      allowBackendWorkspace
    );
  }

  const folders = vscode.workspace.workspaceFolders;
  if (!folders || folders.length === 0) {
    return { workspaceRoot: '', reason: 'not_found' };
  }

  if (folders.length === 1) {
    return enforceBackendWorkspaceGuard(
      { workspaceRoot: folders[0].uri.fsPath, reason: 'single' },
      backendProjectPath,
      allowBackendWorkspace
    );
  }

  const activeEditor = vscode.window.activeTextEditor;
  const activeUri = activeEditor?.document?.uri;
  if (activeUri) {
    const workspaceFolder = vscode.workspace.getWorkspaceFolder(activeUri);
    if (workspaceFolder) {
      return enforceBackendWorkspaceGuard(
        { workspaceRoot: workspaceFolder.uri.fsPath, reason: 'active_file' },
        backendProjectPath,
        allowBackendWorkspace
      );
    }
  }

  return { workspaceRoot: '', reason: 'ambiguous' };
}

function enforceBackendWorkspaceGuard(state, backendProjectPath, allowBackendWorkspace) {
  if (allowBackendWorkspace || !state.workspaceRoot || !backendProjectPath) {
    return state;
  }

  const backendRoot = path.dirname(backendProjectPath);
  if (samePath(state.workspaceRoot, backendRoot)) {
    return {
      workspaceRoot: '',
      reason: 'backend_workspace_blocked',
      blockedWorkspaceRoot: state.workspaceRoot,
      backendProjectPath
    };
  }

  return state;
}

function samePath(left, right) {
  return path.resolve(left).toLowerCase() === path.resolve(right).toLowerCase();
}

module.exports = { resolveWorkspaceRoot };
