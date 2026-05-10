# Local Cursor Agent VS Code Extension

VS Code frontend for LocalCursorAgent backend.

## Run

1. From repository root, build/install extension:
   - `.\scripts\devtools\Update-VSCodeExtension.cmd`
2. In VS Code run `Developer: Reload Window`.
3. Open `LOCAL AGENT` view.

## What it does

- Sends tasks from webview to backend agent runner.
- Resolves workspace mode:
  - existing workspace (default path)
  - empty/no workspace bootstrap via `targetWorkspacePath`.
- Shows structured run result payload:
  - status/summary/failure/timeline
  - changed files
  - diagnostics/build state
  - workspace initialization fields
  - approval/context/indexing/retry diagnostics when present.

## Notes

- Backend connection is wired and active.
- Frontend tests:
  - `cd vscode-extension && npm test`
