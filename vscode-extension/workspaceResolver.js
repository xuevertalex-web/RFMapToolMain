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

    const guardedConfigured = enforceBackendWorkspaceGuard(
      { workspaceRoot: configuredWorkspaceRoot, reason: 'configured', targetWorkspacePath: configuredWorkspaceRoot },
      backendProjectPath,
      allowBackendWorkspace
    );
    if (!guardedConfigured.workspaceRoot) {
      return guardedConfigured;
    }
    if (options.initializeIfMissing === true) {
      return ensureProjectWorkspace(guardedConfigured.workspaceRoot, options.projectNameHint);
    }
    return guardedConfigured;
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

function ensureProjectWorkspace(targetRoot, projectNameHint) {
  const sandboxRoot = path.resolve(targetRoot);
  if (!fs.existsSync(sandboxRoot) || !fs.statSync(sandboxRoot).isDirectory()) {
    return {
      workspaceRoot: '',
      reason: 'configured_not_found',
      targetWorkspacePath: sandboxRoot
    };
  }

  const folderName = buildProjectFolderName(projectNameHint);
  const projectRoot = path.join(sandboxRoot, folderName);
  if (!fs.existsSync(projectRoot)) {
    fs.mkdirSync(projectRoot, { recursive: true });
    return {
      workspaceRoot: projectRoot,
      reason: 'created_from_target_root',
      workspaceInitialized: true,
      workspaceInitializationMode: 'created_from_target_root',
      targetWorkspacePath: sandboxRoot,
      initializedProjectRoot: projectRoot,
      suggestedProjectFolderName: folderName
    };
  }

  return {
    workspaceRoot: projectRoot,
    reason: 'configured',
    workspaceInitialized: false,
    workspaceInitializationMode: 'existing',
    targetWorkspacePath: sandboxRoot,
    initializedProjectRoot: projectRoot,
    suggestedProjectFolderName: folderName
  };
}

function buildProjectFolderName(hint) {
  const raw = String(hint || '').trim();
  const normalized = raw
    .replace(/[^a-zA-Z0-9\-_ ]+/g, ' ')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 4)
    .join('-');
  if (normalized.length >= 3) {
    return normalized;
  }

  const now = new Date();
  const yyyy = now.getFullYear();
  const mm = String(now.getMonth() + 1).padStart(2, '0');
  const dd = String(now.getDate()).padStart(2, '0');
  const hh = String(now.getHours()).padStart(2, '0');
  const min = String(now.getMinutes()).padStart(2, '0');
  return `NewProject-${yyyy}${mm}${dd}-${hh}${min}`;
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
