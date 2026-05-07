# Release Artifacts

Use source-only snapshot for sharing/review:

```powershell
.\scripts\Create-SourceSnapshot.ps1
```

Output archive is created under `.\release\` and contains only tracked source at `HEAD` (no `.git`, `bin`, `obj`, runtime logs, or local artifacts).
