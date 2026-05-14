const path = require('path');
const fs = require('fs');
const { spawn } = require('child_process');
const http = require('http');

let currentAgentProcess = null;
let stopRequested = false;
let lastStructuredResult = null;

function preflightBackendProjectPath(configuredProjectPath, extensionRoot, workspaceRoot) {
  const configured = String(configuredProjectPath || '').trim();
  if (!configured) {
    return { ok: false, code: 'backend_path_empty', message: 'localCursorAgent.backendProjectPath is empty. Set an absolute path to LocalCursorAgent.csproj in VS Code settings.' };
  }
  if (!path.isAbsolute(configured)) {
    return { ok: false, code: 'backend_path_not_absolute', message: 'localCursorAgent.backendProjectPath must be an absolute path to LocalCursorAgent.csproj.' };
  }
  if (!fs.existsSync(configured)) {
    return { ok: false, code: 'backend_path_not_found', message: `Configured backend project not found: ${configured}` };
  }
  if (!configured.toLowerCase().endsWith('.csproj')) {
    return { ok: false, code: 'backend_path_not_csproj', message: 'localCursorAgent.backendProjectPath must point to a .csproj file.' };
  }
  return { ok: true };
}

function resolveAgentProjectPath(workspaceRoot, extensionRoot, configuredProjectPath) {
  const candidates = [
    configuredProjectPath,
    path.join(extensionRoot, '..', 'LocalCursorAgent.csproj'),
    path.join(extensionRoot, '..', '..', 'LocalCursorAgent.csproj'),
    path.join(extensionRoot, '..', '..', 'LocalCursorAgent', 'LocalCursorAgent.csproj'),
    path.join(workspaceRoot, 'LocalCursorAgent.csproj'),
    path.join(workspaceRoot, '..', 'LocalCursorAgent', 'LocalCursorAgent.csproj'),
    path.join(workspaceRoot, 'LocalCursorAgent', 'LocalCursorAgent.csproj')
  ];

  for (const candidate of candidates) {
    if (candidate && fs.existsSync(candidate)) return candidate;
  }

  return '';
}

function runAgent(panel, workspaceRoot, task, output, extensionRoot, configuredProjectPath, selectedModel, sessionContext) {
  return new Promise((resolve, reject) => {
    const preflight = preflightBackendProjectPath(configuredProjectPath, extensionRoot, workspaceRoot);
    if (!preflight.ok) {
      const preflightError = new Error(preflight.message);
      preflightError.result = {
        ok: false,
        finalStatus: 'error',
        reasonCode: preflight.code,
        message: preflight.message,
        summary: 'Backend preflight failed before agent run.'
      };
      reject(preflightError);
      return;
    }
    const projectPath = resolveAgentProjectPath(workspaceRoot, extensionRoot, configuredProjectPath);
    if (!projectPath) {
      reject(new Error('LocalCursorAgent.csproj not found.'));
      return;
    }

    stopRequested = false;
    console.log('WorkspaceRoot:', workspaceRoot);
    const taskWithSessionMemory = composeTaskWithSessionMemory(task, sessionContext);
    const composedTask = clampTaskForProcessArg(composeTaskWithContinuation(taskWithSessionMemory, lastStructuredResult));
    const args = ['run', '--project', projectPath, '--', '--workspace', workspaceRoot, '--task', composedTask];
    const normalizedModel = String(selectedModel || '').trim();
    if (normalizedModel) {
      args.push('--ollama-model', normalizedModel);
    }
    const cwd = path.dirname(projectPath);
    const commandLine = `dotnet ${args.join(' ')}`;

    output.appendLine(`Task: ${task}`);
    if (taskWithSessionMemory !== task) {
      output.appendLine('TaskComposedWithSessionMemory: yes');
    }
    if (composedTask !== task) {
      output.appendLine(`TaskComposedWithContinuation: yes`);
    }
    output.appendLine(`Model: ${normalizedModel || '(default)'}`);
    output.appendLine(`WorkspaceRoot: ${workspaceRoot}`);
    output.appendLine(`ProjectPath: ${projectPath}`);
    output.appendLine(`Command: ${commandLine}`);
    ensureOllamaLazyResume(output, normalizedModel)
      .then(() => {
        output.appendLine('--- Agent output start ---');

        const child = spawn('dotnet', args, {
          cwd,
          shell: false,
          windowsHide: true
        });
        currentAgentProcess = child;

    const logs = [];
    let stdout = '';
    let stderr = '';

        child.stdout.on('data', chunk => {
      const text = chunk.toString();
      stdout += text;
      logs.push(...text.split(/\r?\n/).filter(Boolean));
      output.append(text);
      if (panel && panel.webview) {
        panel.webview.postMessage({
          type: 'agentLog',
          stream: 'stdout',
          text
        });
      }
        });

        child.stderr.on('data', chunk => {
      const text = chunk.toString();
      stderr += text;
      logs.push(...text.split(/\r?\n/).filter(Boolean));
      output.append(text);
      if (panel && panel.webview) {
        panel.webview.postMessage({
          type: 'agentLog',
          stream: 'stderr',
          text
        });
      }
        });

        child.on('error', err => {
      currentAgentProcess = null;
      stopRequested = false;
      const message = err instanceof Error ? err.message : String(err);
      output.appendLine(`--- Agent output failed: spawn error ---`);
      output.appendLine(message);
      reject(new Error(`Failed to start agent process: ${message}`));
        });
        child.on('close', (code, signal) => {
      currentAgentProcess = null;
      output.appendLine(`--- Agent output end ---`);
      output.appendLine(`ExitCode: ${formatExitCode(code)}`);
      if (signal) {
        output.appendLine(`Signal: ${signal}`);
      }

      const structuredResult = extractStructuredResult(logs, stdout, stderr);
      if (structuredResult) {
        lastStructuredResult = structuredResult;
        appendStructuredResultSummary(output, structuredResult);
      }

      if (stopRequested) {
        output.appendLine('Agent stopped by user');
        if (panel && panel.webview) {
          panel.webview.postMessage({ type: 'agentFinished', ok: false, error: 'Agent stopped by user.' });
        }
        stopRequested = false;
        reject(new Error('Agent stopped by user.'));
        return;
      }

      if (code !== 0) {
        stopRequested = false;
        const fallbackResult = structuredResult ? null : buildProcessFailureResult({
          code,
          signal,
          stdout,
          stderr,
          projectPath,
          workspaceRoot,
          cwd,
          commandLine
        });
        const errorText = structuredResult && structuredResult.ok === false && structuredResult.message
          ? structuredResult.message
          : fallbackResult.message;
        const error = new Error(errorText);
        error.result = structuredResult || fallbackResult;
        error.exitCode = code;
        reject(error);
        return;
      }

      if (structuredResult && structuredResult.ok === false) {
        stopRequested = false;
        const error = new Error(structuredResult.message || 'Agent returned a failed result.');
        error.result = structuredResult;
        error.exitCode = code;
        reject(error);
        return;
      }

      stopRequested = false;
          resolve({
        text: structuredResult
          ? JSON.stringify(structuredResult, null, 2)
          : `Agent run completed successfully (exit code ${code}).`,
        logs: logs.length > 0 ? logs : [stdout || stderr || 'No output'],
        result: structuredResult
          });
        });
      })
      .catch(reject);
  });
}

function clampTaskForProcessArg(task) {
  const value = String(task || '').trim();
  if (!value) {
    return value;
  }

  // Keep enough space for other args and paths on Windows CreateProcess.
  const maxChars = 12000;
  if (value.length <= maxChars) {
    return value;
  }

  const head = value.slice(0, maxChars - 160).trimEnd();
  const tail = '\n\n[session-memory trimmed to keep command length safe]';
  return head + tail;
}

function composeTaskWithSessionMemory(task, sessionContext) {
  const rawTask = String(task || '').trim();
  if (!rawTask) {
    return rawTask;
  }
  if (!sessionContext || typeof sessionContext !== 'object' || sessionContext.enabled !== true) {
    return rawTask;
  }

  const profile = sessionContext.profile && typeof sessionContext.profile === 'object'
    ? sessionContext.profile
    : {};
  const maxChars = Number.isFinite(profile.maxPromptCharsFromHistory)
    ? Math.max(2000, Math.min(8000, Math.floor(profile.maxPromptCharsFromHistory)))
    : 6000;

  const parts = [];

  const historySummary = String(sessionContext.historySummary || '').trim();
  if (historySummary) {
    parts.push(`History summary: ${historySummary}`);
  }

  const sessionState = sessionContext.sessionState && typeof sessionContext.sessionState === 'object'
    ? sessionContext.sessionState
    : null;
  if (sessionState) {
    const stateLines = [];
    const lastTask = String(sessionState.lastTask || '').trim();
    const lastStatus = String(sessionState.lastStatus || '').trim();
    const changedFilesCount = Number.isFinite(sessionState.changedFilesCount)
      ? Math.max(0, Math.floor(sessionState.changedFilesCount))
      : 0;
    const nextActions = Array.isArray(sessionState.nextActions)
      ? sessionState.nextActions.map(x => String(x || '').trim()).filter(Boolean).slice(0, 3)
      : [];
    if (lastTask) stateLines.push(`lastTask=${lastTask}`);
    if (lastStatus) stateLines.push(`lastStatus=${lastStatus}`);
    stateLines.push(`changedFiles=${changedFilesCount}`);
    if (nextActions.length > 0) stateLines.push(`nextActions=${nextActions.join(' | ')}`);
    if (stateLines.length > 0) {
      parts.push(`Session state: ${stateLines.join('; ')}`);
    }
  }

  const lastStructured = sessionContext.lastStructuredResultSummary && typeof sessionContext.lastStructuredResultSummary === 'object'
    ? sessionContext.lastStructuredResultSummary
    : null;
  if (lastStructured) {
    const statusText = String(lastStructured.statusText || '').trim();
    const summaryText = String(lastStructured.summaryText || '').trim();
    const resultText = String(lastStructured.resultText || '').trim();
    const changedFilesCount = Number.isFinite(lastStructured.changedFilesCount)
      ? Math.max(0, Math.floor(lastStructured.changedFilesCount))
      : 0;
    const srLines = [];
    if (statusText) srLines.push(`status=${statusText}`);
    if (summaryText) srLines.push(`summary=${summaryText}`);
    if (resultText) srLines.push(`result=${resultText}`);
    srLines.push(`changedFiles=${changedFilesCount}`);
    parts.push(`Last structured result: ${srLines.join('; ')}`);
  }

  const recentMessages = Array.isArray(sessionContext.recentMessages)
    ? sessionContext.recentMessages
    : [];
  const recentLines = [];
  let usedChars = 0;
  for (let i = recentMessages.length - 1; i >= 0; i--) {
    const item = recentMessages[i];
    if (!item || typeof item !== 'object') {
      continue;
    }
    const role = String(item.role || '').trim().toLowerCase() === 'assistant' ? 'assistant' : 'user';
    const text = String(item.text || '').trim();
    if (!text) {
      continue;
    }
    const line = `${role}: ${text}`;
    if (usedChars + line.length > maxChars) {
      break;
    }
    usedChars += line.length;
    recentLines.unshift(line);
  }
  if (recentLines.length > 0) {
    parts.push(`Recent dialog:\n${recentLines.join('\n')}`);
  }

  if (parts.length === 0) {
    return rawTask;
  }

  return [
    'Use the session memory below to stay consistent with previous steps.',
    parts.join('\n\n'),
    `Current user request: ${rawTask}`
  ].join('\n\n');
}

function composeTaskWithContinuation(task, previousResult) {
  const rawTask = String(task || '').trim();
  if (!rawTask) {
    return rawTask;
  }

  const normalized = rawTask.toLowerCase();
  const asciiNormalized = normalized
    .replace(/[^\x00-\x7F]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
  const isContinueIntent =
    normalized === 'continue' ||
    normalized === 'continue.' ||
    normalized === 'next' ||
    normalized === 'resume' ||
    asciiNormalized === 'go on' ||
    asciiNormalized === 'continue' ||
    normalized === 'продолжай' ||
    normalized === 'продолжай.' ||
    normalized === 'давай дальше' ||
    normalized === 'дальше';

  if (!isContinueIntent || !previousResult || typeof previousResult !== 'object') {
    return rawTask;
  }

  const continuationHint = String(previousResult.continuationHint || '').trim();
  const candidates = Array.isArray(previousResult.nextActionCandidates)
    ? previousResult.nextActionCandidates.map(x => String(x || '').trim()).filter(Boolean).slice(0, 3)
    : [];
  const lastStep = previousResult.sessionContinuation && typeof previousResult.sessionContinuation === 'object'
    ? String(previousResult.sessionContinuation.lastSuccessfulStep || '').trim()
    : '';
  const lastAction = previousResult.sessionContinuation && typeof previousResult.sessionContinuation === 'object'
    ? String(previousResult.sessionContinuation.lastKnownAction || '').trim()
    : '';

  const contextLines = [];
  if (continuationHint) contextLines.push(`Continuation hint: ${continuationHint}`);
  if (lastStep) contextLines.push(`Last successful step: ${lastStep}`);
  if (lastAction) contextLines.push(`Last known action: ${lastAction}`);
  if (candidates.length) contextLines.push(`Next action candidates: ${candidates.join(' | ')}`);

  if (contextLines.length === 0) {
    return rawTask;
  }

  return `Continue from previous run.\n${contextLines.join('\n')}\nNow execute the next concrete engineering step.`;
}

function shouldTryOllamaLazyResume(model) {
  const autoStart = String(process.env.LOCAL_CURSOR_AGENT_OLLAMA_AUTOSTART || '1').trim().toLowerCase();
  if (autoStart === '0' || autoStart === 'false') {
    return false;
  }

  const value = String(model || '').trim().toLowerCase();
  if (!value) {
    return true;
  }

  return !value.includes('openai') && !value.includes('gemini');
}

async function ensureOllamaLazyResume(output, selectedModel) {
  if (!shouldTryOllamaLazyResume(selectedModel)) {
    return;
  }

  if (await isOllamaReady()) {
    return;
  }

  output.appendLine('Ollama not reachable, trying lazy-resume via `ollama serve`...');
  try {
    const child = spawn('ollama', ['serve'], {
      detached: true,
      stdio: 'ignore',
      windowsHide: true,
      shell: false
    });
    child.unref();
  } catch {
    output.appendLine('Ollama lazy-resume spawn failed; continuing with normal agent run.');
    return;
  }

  const ready = await waitForOllamaReady(8000);
  output.appendLine(ready ? 'Ollama lazy-resume succeeded.' : 'Ollama still unavailable after lazy-resume wait.');
}

function isOllamaReady() {
  return new Promise(resolve => {
    const req = http.get('http://127.0.0.1:11434/api/tags', res => {
      const ok = typeof res.statusCode === 'number' && res.statusCode >= 200 && res.statusCode < 500;
      res.resume();
      resolve(ok);
    });

    req.setTimeout(700, () => {
      req.destroy();
      resolve(false);
    });
    req.on('error', () => resolve(false));
  });
}

async function waitForOllamaReady(timeoutMs) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await isOllamaReady()) {
      return true;
    }
    await new Promise(resolve => setTimeout(resolve, 400));
  }
  return false;
}

function buildProcessFailureMessage(code, signal, stdout, stderr) {
  const reason = signal
    ? `Agent process was terminated by signal ${signal}.`
    : `Agent process failed with exit code ${formatExitCode(code)}.`;
  const tail = getOutputTail(stderr || stdout);
  return tail ? `${reason}\nLast process output:\n${tail}` : reason;
}

function buildProcessFailureResult(details) {
  const message = buildProcessFailureMessage(details.code, details.signal, details.stdout, details.stderr);
  const exitCodeText = formatExitCode(details.code);
  const stdoutTail = getOutputTail(details.stdout);
  const stderrTail = getOutputTail(details.stderr);
  const diagnostics = [
    ['exitCode', exitCodeText],
    ['signal', details.signal || ''],
    ['workspaceRoot', details.workspaceRoot],
    ['projectPath', details.projectPath],
    ['cwd', details.cwd],
    ['command', details.commandLine],
    ['stderrTail', stderrTail],
    ['stdoutTail', stdoutTail]
  ]
    .filter(([, value]) => String(value || '').trim())
    .map(([code, value]) => ({
      severity: 'error',
      code,
      message: String(value)
    }));

  return {
    ok: false,
    message,
    summary: 'Agent process failed before returning a structured result.',
    workspaceRoot: details.workspaceRoot,
    buildSucceeded: null,
    changedFiles: [],
    changedHints: [],
    changedRanges: [],
    changedKinds: [],
    diagnostics,
    timeline: [
      {
        stage: 'process',
        status: 'failed',
        message: `dotnet exited with ${exitCodeText}`
      }
    ],
    rootCauseCode: 'agent_process_failed',
    failedStage: 'process',
    failedStep: 'dotnet run',
    reasonCode: details.signal ? 'process_signal' : 'non_zero_exit_code',
    explanation: message,
    pipelineStoppedReason: 'backend process did not complete successfully',
    downstreamNotStarted: 'result processing'
  };
}

function formatExitCode(code) {
  if (typeof code !== 'number') {
    return String(code);
  }

  const signedCode = code > 0x7fffffff ? code - 0x100000000 : code;
  return signedCode === code ? String(code) : `${signedCode} (${code})`;
}

function getOutputTail(text) {
  const lines = String(text || '').split(/\r?\n/).map(line => line.trimEnd()).filter(Boolean);
  if (lines.length === 0) {
    return '';
  }

  return lines.slice(-12).join('\n');
}

function extractStructuredResult(lines, stdout, stderr) {
  const markedFromText = extractMarkedStructuredResultFromText(stdout, stderr);
  if (markedFromText) {
    return markedFromText;
  }

  const trailingFromText = extractTrailingStructuredResultFromText(stdout, stderr);
  if (trailingFromText) {
    return trailingFromText;
  }

  const markedResult = extractMarkedStructuredResult(lines);
  if (markedResult) {
    return markedResult;
  }

  for (let i = lines.length - 1; i >= 0; i--) {
    const raw = String(lines[i] || '').trim();
    if (!raw || !raw.startsWith('{') || !raw.endsWith('}')) {
      continue;
    }

    try {
      const parsed = JSON.parse(raw);
      if (parsed && typeof parsed === 'object' && typeof parsed.ok === 'boolean') {
        return parsed;
      }
    } catch {
      // Ignore parse failures and continue scanning earlier lines.
    }
  }

  return null;
}

function extractTrailingStructuredResultFromText(stdout, stderr) {
  const text = [String(stdout || ''), String(stderr || '')].filter(Boolean).join('\n');
  if (!text) {
    return null;
  }

  const marker = '{"ok":';
  let searchStart = 0;
  let lastParsed = null;
  while (searchStart < text.length) {
    const jsonStart = text.indexOf(marker, searchStart);
    if (jsonStart < 0) {
      break;
    }

    const jsonEnd = findJsonObjectEnd(text, jsonStart);
    if (jsonEnd < 0) {
      break;
    }

    const payload = text.slice(jsonStart, jsonEnd + 1);
    try {
      const parsed = JSON.parse(payload);
      if (parsed && typeof parsed === 'object' && typeof parsed.ok === 'boolean') {
        lastParsed = parsed;
      }
    } catch {
      // Ignore parse failures and continue scanning.
    }

    searchStart = jsonEnd + 1;
  }

  return lastParsed;
}

function findJsonObjectEnd(text, startIndex) {
  let depth = 0;
  let inString = false;
  let escaped = false;
  for (let i = startIndex; i < text.length; i++) {
    const ch = text[i];
    if (inString) {
      if (escaped) {
        escaped = false;
      } else if (ch === '\\') {
        escaped = true;
      } else if (ch === '"') {
        inString = false;
      }
      continue;
    }

    if (ch === '"') {
      inString = true;
    } else if (ch === '{') {
      depth++;
    } else if (ch === '}') {
      depth--;
      if (depth === 0) {
        return i;
      }
    }
  }

  return -1;
}

function extractMarkedStructuredResultFromText(stdout, stderr) {
  const text = [String(stdout || ''), String(stderr || '')].filter(Boolean).join('\n');
  if (!text) {
    return null;
  }

  const startMarker = '__LOCAL_CURSOR_AGENT_RESULT_START__';
  const endMarker = '__LOCAL_CURSOR_AGENT_RESULT_END__';
  const endIndex = text.lastIndexOf(endMarker);
  if (endIndex < 0) {
    return null;
  }

  const startIndex = text.lastIndexOf(startMarker, endIndex);
  if (startIndex < 0 || startIndex >= endIndex) {
    return null;
  }

  const payload = text.slice(startIndex + startMarker.length, endIndex).trim();
  if (!payload) {
    return null;
  }

  try {
    const parsed = JSON.parse(payload);
    if (parsed && typeof parsed === 'object' && typeof parsed.ok === 'boolean') {
      return parsed;
    }
  } catch {
    // Ignore parse errors and keep line-based fallback path.
  }

  return null;
}

function extractMarkedStructuredResult(lines) {
  const endIndex = findLastLineIndex(lines, '__LOCAL_CURSOR_AGENT_RESULT_END__');
  if (endIndex < 0) {
    return null;
  }

  const startIndex = findLastLineIndex(lines.slice(0, endIndex), '__LOCAL_CURSOR_AGENT_RESULT_START__');
  if (startIndex < 0 || startIndex >= endIndex) {
    return null;
  }

  const payload = lines.slice(startIndex + 1, endIndex).join('\n').trim();
  if (!payload) {
    return null;
  }

  try {
    const parsed = JSON.parse(payload);
    if (parsed && typeof parsed === 'object' && typeof parsed.ok === 'boolean') {
      return parsed;
    }
  } catch {
    // Fall back to legacy single-line JSON scanning below.
  }

  return null;
}

function findLastLineIndex(lines, value) {
  for (let i = lines.length - 1; i >= 0; i--) {
    if (String(lines[i] || '').trim() === value) {
      return i;
    }
  }

  return -1;
}

function appendStructuredResultSummary(output, structuredResult) {
  if (!output || !structuredResult || typeof structuredResult !== 'object') {
    return;
  }

  const changedFiles = Array.isArray(structuredResult.changedFiles)
    ? structuredResult.changedFiles.filter(Boolean)
    : [];
  const fallbackReason = String(structuredResult.fallbackReason || '').trim();
  const fallbackMode = String(structuredResult.fallbackMode || '').trim();
  const summary = String(structuredResult.summary || '').trim();
  const finalStatus = String(structuredResult.finalStatus || '').trim();
  const status = String(
    finalStatus ||
    (structuredResult.ok
      ? (fallbackReason || fallbackMode ? 'fallback-success' : 'success')
      : 'error')
  ).trim();
  const buildText = typeof structuredResult.buildText === 'string' && structuredResult.buildText.trim()
    ? structuredResult.buildText.trim()
    : typeof structuredResult.buildStarted === 'boolean'
      ? (structuredResult.buildStarted ? (structuredResult.buildSucceeded ? 'succeeded' : 'failed') : 'not started')
      : 'not run';
  const reasonCode = String(
    structuredResult.reasonCode ||
    structuredResult.rootCauseCode ||
    structuredResult.failureCode ||
    ''
  ).trim();
  const modelProvider = String(structuredResult.provider || structuredResult.modelProvider || '').trim();
  const model = String(structuredResult.model || '').trim();
  const modelText = [modelProvider, model].filter(Boolean).join(' / ');
  const embeddingsStatus = String(structuredResult.embeddingsStatus || structuredResult.EmbeddingsStatus || '').trim();
  const continuationHint = String(structuredResult.continuationHint || '').trim();
  const nextActionCandidates = Array.isArray(structuredResult.nextActionCandidates)
    ? structuredResult.nextActionCandidates.map(item => String(item || '').trim()).filter(Boolean).slice(0, 3)
    : [];
  const degradedFlags = Array.isArray(structuredResult.degradedFlags)
    ? structuredResult.degradedFlags.map(item => String(item || '').trim()).filter(Boolean)
    : [];
  const summaryText = summary || String(structuredResult.summaryText || structuredResult.message || '').trim();

  output.appendLine('--- Structured result summary ---');
  output.appendLine(`Status: ${status || 'not available'}`);
  if (finalStatus) {
    output.appendLine(`FinalStatus: ${finalStatus}`);
  }
  if (reasonCode) {
    output.appendLine(`ReasonCode: ${reasonCode}`);
  }
  if (summaryText) {
    output.appendLine(`Summary: ${summaryText}`);
  }
  output.appendLine(`Build: ${buildText}`);
  output.appendLine(`ChangedFiles: ${changedFiles.length}`);
  if (fallbackReason || fallbackMode) {
    output.appendLine(`Fallback: ${[fallbackReason, fallbackMode].filter(Boolean).join(' / ')}`);
  }
  if (modelText) {
    output.appendLine(`Model: ${modelText}`);
  }
  if (embeddingsStatus) {
    output.appendLine(`EmbeddingsStatus: ${embeddingsStatus}`);
  }
  if (continuationHint) {
    output.appendLine(`ContinuationHint: ${continuationHint}`);
  }
  if (nextActionCandidates.length) {
    output.appendLine(`NextActions: ${nextActionCandidates.join(' | ')}`);
  }
  if (degradedFlags.length) {
    output.appendLine(`Degraded: ${degradedFlags.join(', ')}`);
  }
}

function hasRunningProcess() {
  return currentAgentProcess !== null;
}

function stopCurrentAgent(output) {
  if (!currentAgentProcess) {
    return false;
  }

  stopRequested = true;
  if (output) {
    output.appendLine('Agent stop requested');
  }

  currentAgentProcess.kill();
  return true;
}

module.exports = { runAgent, hasRunningProcess, stopCurrentAgent, extractStructuredResult, composeTaskWithContinuation, preflightBackendProjectPath };
