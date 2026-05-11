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

4. Doctor (full diagnostics)

```cmd
.\scripts\devtools\Doctor.cmd
```

Runs:
- basic path/config checks (`LocalCursorAgent.csproj`, VS Code settings keys)
- shows newest root `local-cursor-agent-*.vsix` files
- `SmokeGate.cmd`
- `npm test` in `vscode-extension`
- prints a single PASS/FAIL summary

5. Doctor quick (fast checks only)

```cmd
.\scripts\devtools\Doctor-Quick.cmd
```

Runs:
- basic path/config checks (`LocalCursorAgent.csproj`, VS Code settings keys)
- shows newest root `local-cursor-agent-*.vsix` files
- does not run build/test pipeline
- prints a single PASS/FAIL summary

6. Doctor JSON mode

```cmd
.\scripts\devtools\Doctor.cmd -json
```

Runs:
- same checks as `Doctor.cmd`
- prints machine-readable JSON status payload (`PASS`/`FAIL` and reason when failed)

7. Doctor parallel-run protection (lock)

- Doctor uses repo-local lock file: `.agent-runtime\doctor.lock`
- parallel run fails fast with clear message
- `-json` mode returns a JSON failure with `reason=lock_conflict`
- lock is cleaned up on normal completion and common fail paths

## Notes

- After extension update, run `Developer: Reload Window` in VS Code.
- These scripts are path-safe and can be run from anywhere.
