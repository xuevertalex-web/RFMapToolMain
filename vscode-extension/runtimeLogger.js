const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

function createRuntimeLogger(options = {}) {
  const extensionRoot = String(options.extensionRoot || '');
  const workspaceRoot = normalizeDirectory(options.workspaceRoot);
  const backendProjectPath = String(options.backendProjectPath || '').trim();
  const backendRoot = normalizeDirectory(backendProjectPath ? path.dirname(backendProjectPath) : '');
  const safeFallbackRoot = path.resolve(extensionRoot, '..');
  const baseRoot = workspaceRoot || backendRoot || safeFallbackRoot;
  const runtimeDir = path.join(baseRoot, '.agent-runtime');
  const textLogPath = path.join(runtimeDir, 'agent.log');
  const jsonlLogPath = path.join(runtimeDir, 'agent.jsonl');
  const maxFileSizeBytes = Number.isFinite(options.maxFileSizeBytes) ? Math.max(1024, Math.floor(options.maxFileSizeBytes)) : 1024 * 1024;
  const maxGenerations = Number.isFinite(options.maxGenerations) ? Math.max(1, Math.floor(options.maxGenerations)) : 3;

  function ensureDir() {
    fs.mkdirSync(runtimeDir, { recursive: true });
  }

  function writeLine(level, message, meta) {
    try {
      ensureDir();
      rotateIfNeeded(textLogPath, maxFileSizeBytes, maxGenerations);
      rotateIfNeeded(jsonlLogPath, maxFileSizeBytes, maxGenerations);
      const ts = new Date().toISOString();
      fs.appendFileSync(textLogPath, `[${ts}] [${level}] ${message}\n`, 'utf8');
      const entry = { ts, level, message, ...(meta && typeof meta === 'object' ? { meta: sanitizeMeta(meta) } : {}) };
      fs.appendFileSync(jsonlLogPath, `${JSON.stringify(entry)}\n`, 'utf8');
    } catch (_) {
      // Keep logger fail-open to avoid breaking extension flow.
    }
  }

  return {
    info(message, meta) { writeLine('info', String(message), meta); },
    warn(message, meta) { writeLine('warn', String(message), meta); },
    error(message, meta) { writeLine('error', String(message), meta); }
  };
}

function rotateIfNeeded(filePath, maxFileSizeBytes, maxGenerations) {
  try {
    if (!fs.existsSync(filePath)) return;
    const stat = fs.statSync(filePath);
    if (stat.size < maxFileSizeBytes) return;
    for (let i = maxGenerations; i >= 1; i--) {
      const src = `${filePath}.${i}`;
      if (i === maxGenerations) {
        if (fs.existsSync(src)) fs.unlinkSync(src);
      } else {
        const dest = `${filePath}.${i + 1}`;
        if (fs.existsSync(src)) fs.renameSync(src, dest);
      }
    }
    fs.renameSync(filePath, `${filePath}.1`);
  } catch (_) {
    // Keep fail-open behavior.
  }
}

function normalizeDirectory(value) {
  const text = String(value || '').trim();
  if (!text) return '';
  try {
    const resolved = path.resolve(text);
    if (!fs.existsSync(resolved)) return '';
    if (!fs.statSync(resolved).isDirectory()) return '';
    return resolved;
  } catch (_) {
    return '';
  }
}

function sanitizeMeta(meta) {
  const safe = {};
  const source = String(meta.source || '').trim();
  const reason = String(meta.reason || '').trim();
  if (source) safe.source = source;
  if (reason) safe.reason = reason;
  if (typeof meta.classifierAnalysisOnly === 'boolean') safe.classifierAnalysisOnly = meta.classifierAnalysisOnly;
  if (typeof meta.fallbackAnalysisOnly === 'boolean') safe.fallbackAnalysisOnly = meta.fallbackAnalysisOnly;
  if (typeof meta.analysisOnlyTask === 'boolean') safe.analysisOnlyTask = meta.analysisOnlyTask;
  if (meta.taskCategory) safe.taskCategory = String(meta.taskCategory);
  if (meta.taskLength != null) safe.taskLength = Number(meta.taskLength) || 0;
  if (meta.taskHash) safe.taskHash = String(meta.taskHash);
  if (meta.pathLoggingEnabled === true && meta.targetWorkspacePath) {
    safe.targetWorkspacePath = String(meta.targetWorkspacePath);
  } else if (meta.targetWorkspacePath) {
    safe.targetWorkspacePath = '[redacted]';
  }
  return safe;
}

function buildTaskTelemetry(task) {
  const text = String(task || '').trim();
  if (!text) return { taskCategory: 'empty', taskLength: 0 };
  const lower = text.toLowerCase();
  const category = /\b(create|delete|remove|rename|fix|edit|update|modify|change)\b/.test(lower)
    ? 'mutation'
    : 'analysis_or_chat';
  const taskHash = crypto.createHash('sha256').update(text).digest('hex').slice(0, 12);
  return { taskCategory: category, taskLength: text.length, taskHash };
}

module.exports = { createRuntimeLogger, buildTaskTelemetry };
