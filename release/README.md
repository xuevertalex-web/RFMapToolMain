# Release Artifacts

Use source-only snapshot for sharing/review:

```powershell
.\scripts\Create-SourceSnapshot.ps1
```

Output archive is created under `.\release\` and contains only tracked source at `HEAD` (no `.git`, `bin`, `obj`, runtime logs, or local artifacts).

Validate snapshot contents:

```powershell
.\scripts\Verify-SourceSnapshot.ps1 -ArchivePath .\release\LocalCursorAgent-source-YYYYMMDD-HHMMSS.zip
```

One-step create + verify:

```powershell
.\scripts\Create-AndVerify-SourceSnapshot.ps1
```
