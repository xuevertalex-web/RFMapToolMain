# LocalCursorAgent

Local coding agent with a VS Code webview frontend and .NET backend.

## Quick Start

1. Build/install extension:

```cmd
.\scripts\devtools\Update-VSCodeExtension.cmd
```

2. In VS Code run `Developer: Reload Window`, then open `LOCAL AGENT`.

3. Run smoke/safety baseline:

```cmd
.\scripts\devtools\SmokeGate.cmd
```

## Workspace Behavior

- Existing workspace:
  - if a project folder is already open in VS Code, agent runs in that workspace;
  - workspace root is not overridden;
  - initialization/bootstrap is not re-run.

- Empty/no workspace:
  - if `localCursorAgent.targetWorkspacePath` is configured, agent creates a new project root inside it;
  - if not configured, agent returns a truthful structured result with `workspaceInitializationRequired=true`.

## New Project Bootstrap

When a new workspace is initialized from `targetWorkspacePath`, extension can apply a simple template:
- dotnet intent -> `dotnet new console`
- node intent -> `npm init -y`
- otherwise -> folder only (`templateType=none`)

## Entry Points and Scripts

- Root backward-compatible launcher:
  - `Start-VSCodeAgent.cmd` (shim)
- Actual dev scripts:
  - `scripts/devtools/SmokeGate.cmd`
  - `scripts/devtools/Update-VSCodeExtension.cmd`
  - `scripts/devtools/Update-VSCodeExtension.ps1`
  - `scripts/devtools/Start-VSCodeAgent.cmd`

## Project Layout

- Backend: `Core/`, `Security/`, `Execution/`, `Context/`, `Indexing/`, `LLM/`, `Tools/`
- Tests: `SafetyTests/`
- VS Code extension: `vscode-extension/`
