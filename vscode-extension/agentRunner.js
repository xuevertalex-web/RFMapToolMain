const path = require('path');
const fs = require('fs');
const { spawn } = require('child_process');

let currentAgentProcess = null;
let stopRequested = false;

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

function runAgent(panel, workspaceRoot, task, output, extensionRoot, configuredProjectPath, selectedModel) {
  return new Promise((resolve, reject) => {
    const projectPath = resolveAgentProjectPath(workspaceRoot, extensionRoot, configuredProjectPath);
    if (!projectPath) {
      reject(new Error('LocalCursorAgent.csproj not found.'));
      return;
    }

    stopRequested = false;
    console.log('WorkspaceRoot:', workspaceRoot);
    const args = ['run', '--project', projectPath, '--', '--workspace', workspaceRoot, '--task', task];
    const normalizedModel = String(selectedModel || '').trim();
    if (normalizedModel) {
      args.push('--ollama-model', normalizedModel);
    }
    const cwd = path.dirname(projectPath);
    const commandLine = `dotnet ${args.join(' ')}`;

    output.appendLine(`Task: ${task}`);
    output.appendLine(`Model: ${normalizedModel || '(default)'}`);
    output.appendLine(`WorkspaceRoot: ${workspaceRoot}`);
    output.appendLine(`ProjectPath: ${projectPath}`);
    output.appendLine(`Command: ${commandLine}`);
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

      const structuredResult = extractStructuredResult(logs);

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
  });
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

function extractStructuredResult(lines) {
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

module.exports = { runAgent, hasRunningProcess, stopCurrentAgent, extractStructuredResult };
