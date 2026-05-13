const { isAnalysisOnlyTask } = require('./workspaceTaskClassifier');
const vscode = require('vscode');
const fs = require('fs');
const path = require('path');

function isLowSignalChatFallback(task) {
  const value = String(task || '').trim().toLowerCase();
  if (!value) return true;
  if (/\b(create|delete|remove|rename|fix|edit|update|modify|change|создай|удали|переименуй|исправь|обнови|измени|поменяй|отредактируй)\b/.test(value)) return false;
  if (/^(тут\??|ало|алло|here\??|what can you do|explain this project|опиши проект\??|что тут\??|debug|ввуигп)$/.test(value)) return true;
  const compact = value.replace(/[\s?!.,;:]+/g, '');
  return compact.length > 0 && compact.length <= 8;
}

function buildLowSignalReply() {
  return 'Я на связи. Сформулируй задачу: что создать, изменить или проверить.';
}

function isProjectDescribePrompt(task) {
  const value = String(task || '').trim().toLowerCase().replace(/[?!.,;:]+$/g, '');
  return value.startsWith('опиши проект') || value.startsWith('explain this project') || value.startsWith('describe this project');
}

function safeRead(filePath, maxChars = 6000) {
  try { return fs.readFileSync(filePath, 'utf8').slice(0, maxChars); } catch (_) { return ''; }
}

function collectKeyFiles(workspaceRoot) {
  const rootCandidates = ['README.md', 'readme.md', 'LocalCursorAgent.sln', 'LocalCursorAgent.csproj', 'Program.cs', 'package.json', '.env', 'appsettings.json'];
  const found = rootCandidates.map(name => path.join(workspaceRoot, name)).filter(p => fs.existsSync(p));
  for (const dir of ['Core', 'Context', 'Execution', 'Diagnostics', 'Memory', 'Security']) {
    const abs = path.join(workspaceRoot, dir);
    if (!fs.existsSync(abs) || !fs.statSync(abs).isDirectory()) continue;
    for (const name of fs.readdirSync(abs).filter(n => n.endsWith('.cs') || n.endsWith('.js')).slice(0, 2)) {
      found.push(path.join(abs, name));
    }
  }
  return Array.from(new Set(found)).slice(0, 14);
}

function buildWorkspaceSummary(workspaceRoot) {
  const entries = fs.readdirSync(workspaceRoot, { withFileTypes: true });
  const dirs = entries.filter(e => e.isDirectory()).map(e => e.name).sort((a, b) => a.localeCompare(b)).slice(0, 12);
  const files = entries.filter(e => e.isFile()).map(e => e.name).sort((a, b) => a.localeCompare(b)).slice(0, 12);
  const hidden = entries.filter(e => e.name.startsWith('.')).map(e => e.name).sort((a, b) => a.localeCompare(b));
  const keyFiles = collectKeyFiles(workspaceRoot);
  const corpus = keyFiles.map(p => safeRead(p)).join('\n').toLowerCase();

  const purpose = (corpus.includes('vscode') && corpus.includes('agent'))
    ? 'Расширение VS Code и локальный backend-агент для анализа и выполнения задач.'
    : 'Кодовый проект с backend и инструментальными скриптами.';
  const runHints = [];
  if (fs.existsSync(path.join(workspaceRoot, 'LocalCursorAgent.csproj'))) runHints.push('dotnet run --project LocalCursorAgent.csproj');
  if (fs.existsSync(path.join(workspaceRoot, 'vscode-extension', 'package.json'))) runHints.push('cd vscode-extension && npm test');
  const risks = [];
  if (corpus.includes('executionpolicy bypass')) risks.push('Скрипты используют ExecutionPolicy Bypass; нужен контроль источников.');
  if (corpus.includes('allowbackendworkspace')) risks.push('allowBackendWorkspace может ослабить защиту backend-репозитория.');
  if (corpus.includes('spawn') || corpus.includes('runcommand')) risks.push('Есть выполнение внешних команд; проверь валидацию входов и путей.');
  if (corpus.includes('.env') || corpus.includes('apikey') || corpus.includes('token')) risks.push('Есть признаки работы с секретами; проверь хранение и маскирование.');
  if (risks.length === 0) risks.push('Критичные риски не обнаружены по быстрому статическому обзору; нужен отдельный security-review.');

  return [
    `Кратко по открытому проекту: ${workspaceRoot}`,
    `Назначение: ${purpose}`,
    `Папок в корне: ${dirs.length}. ${dirs.length ? `Примеры: ${dirs.join(', ')}` : ''}`.trim(),
    `Файлов в корне: ${files.length}. ${files.length ? `Примеры: ${files.join(', ')}` : ''}`.trim(),
    `Скрытые элементы: ${hidden.length}. ${hidden.length ? hidden.join(', ') : 'нет'}`,
    `Ключевые файлы для анализа: ${keyFiles.length ? keyFiles.map(p => path.relative(workspaceRoot, p)).join(', ') : 'не обнаружены'}`,
    `Как запускать: ${runHints.length ? runHints.join(' | ') : 'нет явных подсказок в корне проекта'}`,
    `Риски/уязвимости (быстрый обзор): ${risks.join(' ')}`,
    'Если нужно, дальше могу сделать углублённый security-аудит по конкретным файлам.'
  ].join('\n');
}

function createPanelRunController(options) {
  const panel = options.panel;
  const output = options.output;
  const resolveWorkspaceRoot = options.resolveWorkspaceRoot;
  const runAgent = options.runAgent;
  const getIsAgentRunning = options.getIsAgentRunning;
  const setIsAgentRunning = options.setIsAgentRunning;
  const extensionRoot = options.extensionRoot;
  let lastTaskSignature = '';
  let lastTaskAtMs = 0;

  function workspaceErrorText(workspaceState) {
    const reason = String(workspaceState && workspaceState.reason || '');
    if (reason === 'configured_not_found') return 'Configured target workspace path not found';
    if (reason === 'backend_workspace_blocked') return 'Backend workspace is blocked for execute/mutation tasks. Options: 1) Run as analysis-only, 2) Allow backend workspace for this run, 3) Open/choose target workspace.';
    if (reason === 'requires_explicit_initialization') return 'Workspace initialization is required but was not performed automatically. Create/select the project folder explicitly and retry.';
    if (reason === 'not_found') return 'Workspace not found. Set localCursorAgent.targetWorkspacePath in settings for empty VS Code windows.';
    return 'Cannot determine active workspace';
  }

  async function handleSendTask(message) {
    const task = String(message.task || '').trim();
    if (!task) {
      panel.webview.postMessage({ type: 'result', text: 'Task is empty' });
      return;
    }

    const nowMs = Date.now();
    const signature = `${task.toLowerCase()}|${String(message && message.selectedModel || message && message.model || '')}`;
    if (signature === lastTaskSignature && (nowMs - lastTaskAtMs) < 1200) return;
    lastTaskSignature = signature;
    lastTaskAtMs = nowMs;

    if (getIsAgentRunning()) {
      vscode.window.showWarningMessage('Agent is already running');
      return;
    }

    try {
      const classifierAnalysisOnly = isAnalysisOnlyTask(task);
      const fallbackAnalysisOnly = isLowSignalChatFallback(task);
      const analysisOnlyTask = classifierAnalysisOnly || fallbackAnalysisOnly;

      if (isProjectDescribePrompt(task)) {
        const workspaceState = resolveWorkspaceRoot({ initializeIfMissing: false, analysisOnlyTask: true, taskText: task });
        const reply = workspaceState.workspaceRoot
          ? buildWorkspaceSummary(workspaceState.workspaceRoot)
          : 'Не удалось определить текущий workspace. Открой корень проекта или укажи localCursorAgent.targetWorkspacePath.';
        panel.webview.postMessage({ type: 'result', text: reply });
        panel.webview.postMessage({ type: 'agentFinished', ok: true, result: reply, structuredResult: { ok: true, finalStatus: 'success', message: reply, lowSignalRouted: true } });
        return;
      }

      if (fallbackAnalysisOnly) {
        const reply = buildLowSignalReply();
        panel.webview.postMessage({ type: 'result', text: reply });
        panel.webview.postMessage({ type: 'agentFinished', ok: true, result: reply, structuredResult: { ok: true, finalStatus: 'success', message: reply, lowSignalRouted: true } });
        return;
      }

      const workspaceState = resolveWorkspaceRoot({ initializeIfMissing: true, projectNameHint: task, analysisOnlyTask, taskText: task });
      if (!workspaceState.workspaceRoot) {
        postWorkspaceFailure(workspaceErrorText(workspaceState), workspaceState);
        return;
      }

      panel.webview.postMessage({ type: 'runningState', running: true });
      setIsAgentRunning(true);
      vscode.window.setStatusBarMessage('Agent running...', 0);
      output.appendLine('Agent run started');

      const selectedModel = String(message.selectedModel || message.model || '').trim();
      const result = await runAgent(panel, workspaceState.workspaceRoot, task, output, extensionRoot, selectedModel, message && typeof message.sessionContext === 'object' ? message.sessionContext : null);
      const resultText = (result.result && result.result.message) || result.text;
      panel.webview.postMessage({ type: 'result', text: resultText });
      panel.webview.postMessage({ type: 'agentFinished', ok: true, result: resultText || 'Agent run completed successfully.', structuredResult: { ...(result.result || {}) } });
      vscode.window.setStatusBarMessage('Agent finished', 3000);
    } catch (err) {
      const text = err instanceof Error ? err.message : String(err);
      const structuredResult = err && typeof err === 'object' ? err.result : null;
      if (text !== 'Agent stopped by user.') {
        const resultText = (structuredResult && structuredResult.message) || `Failed to run agent: ${text}`;
        panel.webview.postMessage({ type: 'result', text: resultText });
        panel.webview.postMessage({ type: 'agentFinished', ok: false, error: text, result: resultText || undefined, structuredResult: structuredResult || null });
      }
      output.appendLine(text);
    } finally {
      panel.webview.postMessage({ type: 'runningState', running: false });
      setIsAgentRunning(false);
      output.appendLine('Agent run finished');
    }
  }

  function postWorkspaceFailure(text, workspaceState) {
    panel.webview.postMessage({ type: 'result', text });
    panel.webview.postMessage({
      type: 'agentFinished',
      ok: false,
      error: text,
      structuredResult: {
        ok: false,
        finalStatus: 'error',
        message: text,
        workspaceInitializationRequired: true,
        workspaceInitialized: false,
        workspaceInitializationMode: 'requires_user_selection',
        targetWorkspacePath: String(workspaceState && workspaceState.targetWorkspacePath || ''),
        initializedProjectRoot: String(workspaceState && workspaceState.initializedProjectRoot || ''),
        suggestedProjectFolderName: String(workspaceState && workspaceState.suggestedProjectFolderName || 'NewProject'),
        projectTemplateApplied: false,
        templateType: 'none'
      }
    });
  }

  return { handleSendTask };
}

module.exports = { createPanelRunController };
