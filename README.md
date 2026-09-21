# Project Superhuman Copilot

Native Windows companion for Project Superhuman.

## v0.1
- Windows system-tray app
- Self-contained single-file EXE
- Automatically starts at Windows sign-in after first launch
- Measures Windows uptime
- Stores a local status snapshot once per minute
- Same-LAN status endpoint protected by a random pairing token
- No cloud dependency
- Does not collect browser history, files, keystrokes, microphone, or camera data

## Build / download
Every push to main runs **Build Windows EXE** in GitHub Actions.

Open **Actions > Build Windows EXE > latest successful run > Artifacts > SuperhumanCopilot-Windows-x64** and extract `SuperhumanCopilot.exe`.

The executable is currently unsigned, so Windows may show a SmartScreen warning.

## Pairing
Run the EXE. It lives in the system tray. Right-click and choose **Copy pairing info**.

The phone and PC must be on the same local network. If Windows Firewall prompts, permit Private networks only.

## Local data
`%LOCALAPPDATA%\ProjectSuperhuman\Copilot\`

## API
- `GET /health`
- `GET /status` with `Authorization: Bearer <pairing token>`

v0.1 measures uptime. Later versions can separately track awake, unlocked, idle, and active-use time.
