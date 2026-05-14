const vscode = require('vscode');
const fs = require('fs');
const path = require('path');

function resolveWorkspaceRoot(options = {}) {
  const configuredWorkspaceRoot = String(options.configuredWorkspaceRoot || '').trim();
  const backendProjectPath = String(options.backendProjectPath || '').trim();
  const allowBackendWorkspace = options.allowBackendWorkspace === true;
  const taskText = String(options.taskText || '').trim();

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
      allowBackendWorkspace,
      taskText
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
      allowBackendWorkspace,
      taskText
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
        allowBackendWorkspace,
        taskText
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
    return {
      workspaceRoot: '',
      reason: 'requires_explicit_initialization',
      workspaceInitialized: false,
      workspaceInitializationRequired: true,
      workspaceInitializationMode: 'requires_explicit_initialization',
      targetWorkspacePath: sandboxRoot,
      initializedProjectRoot: projectRoot,
      suggestedProjectFolderName: folderName,
      projectTemplateApplied: false,
      templateType: 'none'
    };
  }

  return {
    workspaceRoot: projectRoot,
    reason: 'configured',
    workspaceInitialized: false,
    workspaceInitializationMode: 'existing',
    targetWorkspacePath: sandboxRoot,
    initializedProjectRoot: projectRoot,
    suggestedProjectFolderName: folderName,
    projectTemplateApplied: false,
    templateType: 'none'
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

function isBackendWorkspaceAllowedByTask(taskText) {
  const value = String(taskText || '').trim().toLowerCase();
  if (!value) return true;

  const explicitMutationEn = /\b(create|delete|remove|rename|fix|edit|update|modify|change)\b/;
  const explicitMutationRu = [
    '\u0441\u043e\u0437\u0434\u0430\u0439',
    '\u0443\u0434\u0430\u043b\u0438',
    '\u043f\u0435\u0440\u0435\u0438\u043c\u0435\u043d\u0443\u0439',
    '\u0438\u0441\u043f\u0440\u0430\u0432\u044c',
    '\u043e\u0431\u043d\u043e\u0432\u0438',
    '\u0438\u0437\u043c\u0435\u043d\u0438',
    '\u043f\u043e\u043c\u0435\u043d\u044f\u0439',
    '\u043e\u0442\u0440\u0435\u0434\u0430\u043a\u0442\u0438\u0440\u0443\u0439'
  ];
  const hasExplicitMutation = explicitMutationEn.test(value) || explicitMutationRu.some(term => value.includes(term));
  if (!hasExplicitMutation) return true;

  const noTargetMutation = /\b(fix it|make it better)\b/;
  if (noTargetMutation.test(value)) return true;

  const hasFileTarget = /(^|[\s"'`])(?:[\w.-]+[\\/])*[\w.-]+\.(?:cs|js|ts|json|md|cmd|ps1|csproj|sln|html|css|yml|yaml|xml|txt|config)(?=$|[\s"'`.,;:!?])/i;
  const hasGenericFileWord = /\b(file|files)\b/.test(value) || /\b\u0444\u0430\u0439\u043b|\u0444\u0430\u0439\u043b\u044b\b/.test(value);
  return !(hasFileTarget.test(value) || hasGenericFileWord);
}

function enforceBackendWorkspaceGuard(state, backendProjectPath, allowBackendWorkspace, taskText) {
  if (allowBackendWorkspace || !state.workspaceRoot || !backendProjectPath) {
    return state;
  }

  const backendRoot = path.dirname(backendProjectPath);
  if (samePath(state.workspaceRoot, backendRoot)) {
    if (isBackendWorkspaceAllowedByTask(taskText)) {
      return state;
    }
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
  return canonicalPath(left) === canonicalPath(right);
}

function canonicalPath(value) {
  const raw = String(value || '').trim();
  if (!raw) return '';
  const resolved = path.resolve(raw);
  try {
    const nativeRealpath = fs.realpathSync && typeof fs.realpathSync.native === 'function'
      ? fs.realpathSync.native(resolved)
      : fs.realpathSync(resolved);
    return path.resolve(nativeRealpath).toLowerCase();
  } catch (_) {
    try {
      return path.resolve(fs.realpathSync(resolved)).toLowerCase();
    } catch (_) {
      return resolved.toLowerCase();
    }
  }
}

module.exports = { resolveWorkspaceRoot, canonicalPath };
