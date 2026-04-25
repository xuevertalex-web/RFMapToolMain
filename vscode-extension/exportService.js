const vscode = require('vscode');
const path = require('path');
const fs = require('fs');

function createExportService(options) {
  const resolveWorkspaceRoot = options.resolveWorkspaceRoot;
  const extensionRoot = options.extensionRoot;
  const output = options.output;

  async function exportRunReport(payload) {
    if (!hasStructuredResultPayload(payload)) {
      vscode.window.showErrorMessage('No structured result to export');
      return;
    }

    try {
      const saveUri = await vscode.window.showSaveDialog({
        defaultUri: vscode.Uri.file(path.join(resolveWorkspaceRoot().workspaceRoot || extensionRoot, 'agent-run-report.md')),
        filters: { Markdown: ['md'] },
        saveLabel: 'Save Run Report'
      });

      if (!saveUri) {
        return;
      }

      const markdown = buildRunReportMarkdown(payload);
      const jsonPath = deriveJsonPathFromMarkdownPath(saveUri.fsPath);
      await fs.promises.writeFile(saveUri.fsPath, markdown, 'utf8');
      await fs.promises.writeFile(jsonPath, JSON.stringify(payload, null, 2), 'utf8');
      vscode.window.showInformationMessage('Run report exported (.md + .json)');
    } catch (err) {
      const text = err instanceof Error ? err.message : String(err);
      vscode.window.showErrorMessage('Failed to export run report');
      output.appendLine(text);
    }
  }

  async function exportLogs(text) {
    const value = String(text || '');
    if (!value.trim()) {
      return;
    }

    try {
      const saveUri = await vscode.window.showSaveDialog({
        defaultUri: vscode.Uri.file(path.join(resolveWorkspaceRoot().workspaceRoot || extensionRoot, 'agent-run-logs.txt')),
        filters: { Text: ['txt'] },
        saveLabel: 'Save Logs'
      });

      if (!saveUri) {
        return;
      }

      await fs.promises.writeFile(saveUri.fsPath, value, 'utf8');
      vscode.window.showInformationMessage('Logs exported');
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      vscode.window.showErrorMessage('Failed to export logs');
      output.appendLine(message);
    }
  }

  async function exportChangedFiles(text) {
    const value = String(text || '');
    if (!value.trim()) {
      vscode.window.showErrorMessage('No changed files to export');
      return;
    }

    try {
      const saveUri = await vscode.window.showSaveDialog({
        defaultUri: vscode.Uri.file(path.join(resolveWorkspaceRoot().workspaceRoot || extensionRoot, 'changed-files.txt')),
        filters: { Text: ['txt'] },
        saveLabel: 'Save Changed Files'
      });

      if (!saveUri) {
        return;
      }

      await fs.promises.writeFile(saveUri.fsPath, value, 'utf8');
      vscode.window.showInformationMessage('Changed files exported');
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      vscode.window.showErrorMessage('Failed to export changed files');
      output.appendLine(message);
    }
  }

  return {
    exportChangedFiles,
    exportLogs,
    exportRunReport
  };
}

function hasStructuredResultPayload(payload) {
  if (!payload || typeof payload !== 'object') {
    return false;
  }

  return !!(
    String(payload.resultText || '').trim() ||
    String(payload.summaryText || '').trim() ||
    String(payload.buildText || '').trim() ||
    (Array.isArray(payload.changedFiles) && payload.changedFiles.length > 0) ||
    (Array.isArray(payload.changedHints) && payload.changedHints.length > 0) ||
    (Array.isArray(payload.changedRanges) && payload.changedRanges.length > 0) ||
    (Array.isArray(payload.changedKinds) && payload.changedKinds.length > 0) ||
    payload.isError
  );
}

function buildRunReportMarkdown(payload) {
  const data = payload && typeof payload === 'object' ? payload : {};
  const lines = [];
  const isError = !!data.isError;

  lines.push('# Agent Run Report');
  lines.push('');
  lines.push('## Status');
  lines.push(isError ? 'Error' : 'OK');
  lines.push('');

  if (data.statusText) {
    lines.push('## Result');
    lines.push(String(data.statusText).trim());
    lines.push('');
  }

  if (data.resultText) {
    lines.push('## Result Text');
    lines.push(String(data.resultText).trim());
    lines.push('');
  }

  if (data.summaryText) {
    lines.push('## Summary');
    lines.push(String(data.summaryText).trim());
    lines.push('');
  }

  lines.push('## Build');
  lines.push(data.buildText ? String(data.buildText).trim() : 'Not run');
  lines.push('');

  lines.push('## Changed Files');
  appendSimpleList(lines, data.changedFiles);
  lines.push('');

  lines.push('## Changed Hints');
  appendObjectList(lines, data.changedHints, hint => {
    const file = String(hint.file || hint.path || hint.filePath || '');
    const text = String(hint.hint || hint.text || '').trim();
    return file || text ? [file, text].filter(Boolean).join(': ') : '';
  });
  lines.push('');

  lines.push('## Changed Kinds');
  appendObjectList(lines, data.changedKinds, kind => {
    const file = String(kind.file || kind.path || kind.filePath || '');
    const value = String(kind.kind || kind.type || '').trim();
    return file || value ? [file, value].filter(Boolean).join(': ') : '';
  });

  if (Array.isArray(data.changedRanges) && data.changedRanges.length > 0) {
    lines.push('');
    lines.push('## Changed Ranges');
    for (const range of data.changedRanges) {
      if (!range || typeof range !== 'object') {
        continue;
      }

      const file = String(range.file || range.path || range.filePath || '');
      const startLine = Number.isFinite(Number(range.startLine)) ? Number(range.startLine) : null;
      const endLine = Number.isFinite(Number(range.endLine)) ? Number(range.endLine) : null;
      const rangeText = startLine !== null
        ? (endLine !== null && endLine !== startLine ? `${startLine}-${endLine}` : `${startLine}`)
        : '';

      if (file || rangeText) {
        lines.push('- ' + [file, rangeText].filter(Boolean).join(': '));
      }
    }
  }

  if (data.timestamp) {
    lines.push('');
    lines.push('## Timestamp');
    lines.push(String(data.timestamp));
  }

  return lines.join('\n').trim() + '\n';
}

function appendSimpleList(lines, values) {
  const list = Array.isArray(values) ? values : [];
  if (list.length === 0) {
    lines.push('- None');
    return;
  }

  for (const item of list) {
    lines.push('- ' + String(item));
  }
}

function appendObjectList(lines, values, formatItem) {
  const list = Array.isArray(values) ? values : [];
  if (list.length === 0) {
    lines.push('- None');
    return;
  }

  let appended = 0;
  for (const item of list) {
    if (!item || typeof item !== 'object') {
      continue;
    }

    const text = formatItem(item);
    if (text) {
      lines.push('- ' + text);
      appended += 1;
    }
  }

  if (appended === 0) {
    lines.push('- None');
  }
}

function deriveJsonPathFromMarkdownPath(markdownPath) {
  const value = String(markdownPath || '').trim();
  if (!value) {
    return value;
  }

  if (/\.md$/i.test(value)) {
    return value.replace(/\.md$/i, '.json');
  }

  return value + '.json';
}

module.exports = {
  createExportService,
  hasStructuredResultPayload
};
