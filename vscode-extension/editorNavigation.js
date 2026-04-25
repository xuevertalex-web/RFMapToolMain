const vscode = require('vscode');
const path = require('path');
const fs = require('fs');

const CHANGED_RANGE_DECORATION_TIMEOUT_MS = 6000;

function createEditorNavigationService(options) {
  const resolveWorkspaceRoot = options.resolveWorkspaceRoot;
  const output = options.output;
  const changedRangeDecoration = options.changedRangeDecoration;

  let lastDecoratedEditor = null;
  let changedRangeDecorationTimer = null;

  function attachLifecycle(context) {
    context.subscriptions.push(
      vscode.window.onDidChangeActiveTextEditor(editor => {
        if (!lastDecoratedEditor) {
          return;
        }

        if (!editor || editor.document.uri.fsPath !== lastDecoratedEditor.document.uri.fsPath) {
          clearChangedRangeDecoration(lastDecoratedEditor);
          lastDecoratedEditor = null;
          clearChangedRangeDecorationTimer();
        }
      }),
      vscode.workspace.onDidCloseTextDocument(document => {
        if (lastDecoratedEditor && lastDecoratedEditor.document && lastDecoratedEditor.document.uri.fsPath === document.uri.fsPath) {
          clearChangedRangeDecoration(lastDecoratedEditor);
          lastDecoratedEditor = null;
          clearChangedRangeDecorationTimer();
        }
      })
    );
  }

  async function openFile(filePath, requestedStartLine, requestedEndLine, silent) {
    const normalizedPath = String(filePath || '').trim();
    if (!normalizedPath) {
      return;
    }

    const workspaceState = resolveWorkspaceRoot();
    const workspaceRoot = workspaceState.workspaceRoot;
    if (!workspaceRoot) {
      showWorkspaceNotFound(!!silent);
      return;
    }

    const targetPath = path.isAbsolute(normalizedPath)
      ? normalizedPath
      : path.join(workspaceRoot, normalizedPath);

    if (!fs.existsSync(targetPath)) {
      const message = `${silent ? 'Changed file' : 'File'} not found: ${targetPath}`;
      if (silent) {
        vscode.window.showWarningMessage(message);
      } else {
        vscode.window.showErrorMessage(message);
      }
      return;
    }

    const activeEditor = vscode.window.activeTextEditor;
    if (activeEditor && activeEditor.document && activeEditor.document.uri && activeEditor.document.uri.fsPath === targetPath) {
      return;
    }

    const uri = vscode.Uri.file(targetPath);
    const document = await vscode.workspace.openTextDocument(uri);
    const viewColumn = activeEditor ? vscode.ViewColumn.Beside : vscode.ViewColumn.Active;
    const editor = await vscode.window.showTextDocument(document, { preview: false, viewColumn });
    const revealRange = resolveRevealRange(document, requestedStartLine, requestedEndLine);
    const targetRange = new vscode.Range(
      revealRange.startLine,
      0,
      revealRange.endLine,
      document.lineAt(revealRange.endLine).text.length
    );

    if (lastDecoratedEditor && lastDecoratedEditor !== editor) {
      clearChangedRangeDecoration(lastDecoratedEditor);
      clearChangedRangeDecorationTimer();
    }

    editor.selection = new vscode.Selection(targetRange.start, targetRange.start);
    editor.revealRange(targetRange, vscode.TextEditorRevealType.InCenter);
    editor.setDecorations(changedRangeDecoration, [targetRange]);
    lastDecoratedEditor = editor;
    scheduleChangedRangeDecorationClear(editor);
  }

  async function openAllChangedFiles(files) {
    const entries = Array.isArray(files) ? files : [];
    if (entries.length === 0) {
      return;
    }

    const workspaceState = resolveWorkspaceRoot();
    const workspaceRoot = workspaceState.workspaceRoot;
    if (!workspaceRoot) {
      vscode.window.showErrorMessage('Workspace not found');
      return;
    }

    for (const fileInfo of entries) {
      if (!fileInfo || typeof fileInfo !== 'object') {
        continue;
      }

      const requestedStartLine = Number.isFinite(Number(fileInfo.startLine)) ? Number(fileInfo.startLine) : null;
      const requestedEndLine = Number.isFinite(Number(fileInfo.endLine)) ? Number(fileInfo.endLine) : null;

      try {
        await openFile(fileInfo.path, requestedStartLine, requestedEndLine, true);
      } catch (err) {
        const text = err instanceof Error ? err.message : String(err);
        vscode.window.showWarningMessage('Changed file not found: ' + String(fileInfo.path || ''));
        output.appendLine(text);
      }
    }
  }

  function dispose() {
    clearChangedRangeDecoration(lastDecoratedEditor);
    lastDecoratedEditor = null;
    clearChangedRangeDecorationTimer();
  }

  function showWorkspaceNotFound(silent) {
    if (silent) {
      vscode.window.showWarningMessage('Workspace not found');
    } else {
      vscode.window.showErrorMessage('Workspace not found');
    }
  }

  function resolveRevealRange(document, requestedStartLine, requestedEndLine) {
    const maxLine = Math.max(0, document.lineCount - 1);

    if (requestedStartLine !== null && requestedStartLine !== undefined) {
      const startLine = clampLineIndex(Number(requestedStartLine) - 1, maxLine);
      const endLine = requestedEndLine !== null && requestedEndLine !== undefined
        ? clampLineIndex(Number(requestedEndLine) - 1, maxLine)
        : startLine;

      return {
        startLine,
        endLine: Math.max(startLine, endLine)
      };
    }

    let targetLine = 0;
    for (let i = 0; i < document.lineCount; i++) {
      if (document.lineAt(i).text.trim().length > 0) {
        targetLine = i;
        break;
      }
    }

    return {
      startLine: targetLine,
      endLine: targetLine
    };
  }

  function clampLineIndex(line, maxLine) {
    if (!Number.isFinite(line)) {
      return 0;
    }

    return Math.max(0, Math.min(maxLine, Math.floor(line)));
  }

  function clearChangedRangeDecoration(editor) {
    try {
      if (editor) {
        editor.setDecorations(changedRangeDecoration, []);
      }
    } catch {
      // Ignore stale editors.
    }
  }

  function clearChangedRangeDecorationTimer() {
    if (changedRangeDecorationTimer !== null) {
      clearTimeout(changedRangeDecorationTimer);
      changedRangeDecorationTimer = null;
    }
  }

  function scheduleChangedRangeDecorationClear(editor) {
    clearChangedRangeDecorationTimer();
    changedRangeDecorationTimer = setTimeout(() => {
      clearChangedRangeDecoration(editor);
      if (lastDecoratedEditor === editor) {
        lastDecoratedEditor = null;
      }
      changedRangeDecorationTimer = null;
    }, CHANGED_RANGE_DECORATION_TIMEOUT_MS);
  }

  return {
    attachLifecycle,
    openAllChangedFiles,
    openFile,
    dispose
  };
}

module.exports = { createEditorNavigationService };
