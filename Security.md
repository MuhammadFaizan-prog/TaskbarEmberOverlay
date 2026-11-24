# Security & Safety Model (Security)

## Threat Model & Boundaries
- **Network Boundaries**:
  - The application is 100% offline. Zero network calls, zero telemetry, zero analytics, zero external API connections.
- **Privilege Requirements**:
  - Operates strictly under standard user privileges. Does NOT request or require UAC elevation or administrator tokens.
- **File System Access**:
  - Reads/writes strictly to `%LOCALAPPDATA%\TaskbarEmberOverlay\settings.json`.
  - Never modifies OS system files, Windows Explorer binaries, PowerShell profiles, or user media.
- **Process Communication (IPC)**:
  - Local named pipe `TaskbarEmberOverlay_Ipc` scoped to current user session (`PipeOptions.CurrentUserOnly`).
  - Supported commands are strictly whitelisted string tokens (`EXIT`, `PAUSE`, `RESUME`, `PING`). No arbitrary code execution or deserialization vulnerability.
- **Windows API Safety**:
  - Read-only window querying (`FindWindow`, `GetWindowRect`, `IsWindowVisible`).
  - Modification is strictly limited to the application's own overlay `HWND` instances.
