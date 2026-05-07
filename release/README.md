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

Dry-run report for snapshot contents:

```powershell
.\scripts\Report-SourceSnapshotContents.ps1 -ArchivePath .\release\LocalCursorAgent-source-YYYYMMDD-HHMMSS.zip
```

One-step create + verify:

```powershell
.\scripts\Create-AndVerify-SourceSnapshot.ps1
```

One-step create + report + verify:

```powershell
.\scripts\Create-Report-Verify-SourceSnapshot.ps1
```

List generated source snapshots:

```powershell
.\scripts\List-SourceSnapshots.ps1
```
