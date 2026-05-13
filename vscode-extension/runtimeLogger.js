const fs = require('fs');
const path = require('path');

function createRuntimeLogger(options = {}) {
  const extensionRoot = String(options.extensionRoot || '');
  const workspaceRoot = String(options.workspaceRoot || '').trim();
  const backendProjectPath = String(options.backendProjectPath || '').trim();
  const backendRoot = backendProjectPath ? path.dirname(backendProjectPath) : '';

  const baseRoot = workspaceRoot || backendRoot || path.resolve(extensionRoot, '..');
  const runtimeDir = path.join(baseRoot, '.agent-runtime');
  const textLogPath = path.join(runtimeDir, 'agent.log');
  const jsonlLogPath = path.join(runtimeDir, 'agent.jsonl');

  function ensureDir() {
    fs.mkdirSync(runtimeDir, { recursive: true });
  }

  function writeLine(level, message, meta) {
    try {
      ensureDir();
      const ts = new Date().toISOString();
      fs.appendFileSync(textLogPath, `[${ts}] [${level}] ${message}\n`, 'utf8');
      const entry = { ts, level, message, ...(meta && typeof meta === 'object' ? { meta } : {}) };
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

module.exports = { createRuntimeLogger };
