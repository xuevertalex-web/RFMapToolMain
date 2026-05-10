# Dev Checklist

## Baseline before any change

1. Check local diff:
   - `git status --short`
2. Run backend smoke/safety:
   - `.\scripts\devtools\SmokeGate.cmd`
3. Run extension tests:
   - `cd vscode-extension && npm test`

Both test commands must pass before and after each change package.

## Packaging discipline

- Keep changes in narrow modular commits.
- Do not mix unrelated files/scopes in one commit.
- If baseline is already red, record it first and do not start a new feature package.

## Workspace safety rule

- Existing workspace flow must not be broken.
- Empty/no workspace flow must stay truthful:
  - with `targetWorkspacePath` -> initialize project root;
  - without it -> `workspaceInitializationRequired=true`, no fake success.

## Script/entrypoint safety

- Keep root `Start-VSCodeAgent.cmd` as backward-compatible shim.
- Actual script implementations live in `scripts/devtools/`.

## Guardrails

- Do not add production hacks for external environment failures.
- Tool/runtime environment issues must not trigger broad production rewrites.
