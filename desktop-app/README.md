# Aelivar Desktop App

## Start

```powershell
cd desktop-app
npm install
npm start
```

## Requirements

- `dotnet build` must have been run for the backend so that:
  - `..\bin\Debug\net8.0\LocalCursorAgent.dll` exists
- Node.js must be installed

## MVP Features

- Open a workspace folder
- Select access mode
- Run/stop Aelivar
- Stream stdout/stderr
- Preview files in the selected workspace
- Show changed files from structured agent output
