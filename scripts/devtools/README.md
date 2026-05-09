# Devtools

Utilities for local development and extension updates.

## Commands

1. Smoke gate

```cmd
.\scripts\devtools\SmokeGate.cmd
```

Runs:
- `dotnet build` for backend
- `dotnet run --project SafetyTests\SafetyTests.csproj`
- baseline smoke PASS checks

2. VS Code extension update

```cmd
.\scripts\devtools\Update-VSCodeExtension.cmd
```

Runs:
- patch version bump in `vscode-extension/package.json`
- VSIX packaging
- force install into VS Code
- keeps only 2 newest `local-cursor-agent-*.vsix` in project root

3. Start VS Code + update extension

```cmd
.\scripts\devtools\Start-VSCodeAgent.cmd
```

Runs:
- extension update script first
- opens VS Code on repository root

## Notes

- After extension update, run `Developer: Reload Window` in VS Code.
- These scripts are path-safe and can be run from anywhere.
