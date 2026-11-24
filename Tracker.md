# Active Task Tracker (Tracker)

## Active State
- **Status**: Completed & Verified
- **Build Target**: .NET 10.0 (WPF) / Windows 10/11 x64
- **Artifact**: `artifacts\publish\win-x64\TaskbarEmberOverlay.exe`

## Stories & Tasks

| ID | Title | Status | Priority | Verification Evidence |
|---|---|---|---|---|
| ST-01 | Reference video inspection & visual criteria | Done | P0 | Sampled frames; confirmed warm ember profile & strict 0% interior fill requirement. |
| ST-02 | Project documentation system (9 docs) | Done | P0 | PRD, Techspec, Appflow, Schema, Plan, Tracker, Rules, Security, Structure created. |
| ST-03 | Visual proof: Rendered taskbar preview on checkerboard | Done | P0 | Generated `proof_on_checkerboard.png` and `proof_on_mock_taskbar.png`. Max interior alpha = 0. |
| ST-04 | Solution scaffolding (`.sln`, app csproj, test csproj) | Done | P0 | Created `TaskbarEmberOverlay.sln`, `src/TaskbarEmberOverlay`, `tests/TaskbarEmberOverlay.Tests`. |
| ST-05 | Win32 Interop & non-activating click-through window | Done | P0 | Implemented `WS_EX_TRANSPARENT`, `WS_EX_LAYERED`, `WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW`, `WM_NCHITTEST` -> `HTTRANSPARENT`. |
| ST-06 | Multi-monitor taskbar detection & auto-hide tracking | Done | P0 | Implemented `TaskbarTrackerService`: tracks `Shell_TrayWnd`, `Shell_SecondaryTrayWnd`, DPI, autohide, fullscreen app detection, Explorer restart recovery. |
| ST-07 | Procedural ember animation engine with exclusion clip | Done | P0 | Implemented `EmberEngine` and `OverlayWindow`: `CombinedGeometry` exclusion clip, crackles, sparks, 30fps cap. |
| ST-08 | Configuration service (`settings.json`) | Done | P1 | Implemented `SettingsService` at `%LOCALAPPDATA%\TaskbarEmberOverlay\settings.json`. |
| ST-09 | Single-instance lock & IPC (`--exit`) | Done | P0 | Implemented named mutex and named pipe IPC with fallback event; verified clean `--exit` shutdown. |
| ST-10 | Unit test suite | Done | P0 | 31/31 tests passed in `TaskbarEmberOverlay.Tests`. |
| ST-11 | Release publish & live system verification | Done | P0 | Published self-contained `win-x64` executable; verified 48MB RAM, <0.1% CPU, graceful exit. |
| ST-12 | Free space detection & side animation | Done | P0 | Implemented `TaskbarLayoutDetector` and `UpdateFreeSpaces`. Automatically detects taskbar free space (left and right wings) and renders full-height crimson smoke & criss-crossing molten needles inspired by `side animation for taskbar.mp4` while keeping all icons 100% clear and clickable. |
| ST-13 | Always-on taskbar visibility & Z-order fix | Done | P0 | Fixed desktop wallpaper `WorkerW`/`Progman`/`explorer.exe` being mistaken for fullscreen games; window set to `HWND_TOPMOST` with `Topmost="True"` so animation is always visible without clicking the taskbar. |
| ST-14 | Dynamic AppList tracking for unpin/pin | Done | P0 | Targeted UIA queries on `StartButton`, `AppList` child buttons, and `WidgetsButton`. Dynamically tracks when apps are unpinned/pinned, expanding the animation to fill extra space every 1.5s with zero cut black strips. |
| ST-15 | Exact reference video side animation & Luma-key alpha | Done | P0 | Extracted 60 high-definition frames from reference video `19.11.2026_23.22.40_REC.mp4` / `side animation for taskbar.mp4` and processed them with luma-key transparency and feathered horizontal opacity masks. Plays smoothly at 20fps with no black borders. |

## Measurements & Benchmarks
- **Test Results**: 31 passed, 0 failed, 0 skipped.
- **Memory Footprint**: ~48.54 MB WorkingSet.
- **CPU Usage**: < 0.1% active, 0.0% paused/hidden/static.
- **Click-Through**: 100% transparent hit testing to taskbar beneath.
- **Side Animation**: Plays exact video frames in left & right taskbar wings; dynamically fills space when apps are unpinned.
- **Reversibility**: Graceful `--exit` tested; uninstallation removes only `%LOCALAPPDATA%\Programs\TaskbarEmberOverlay` and `%LOCALAPPDATA%\TaskbarEmberOverlay`.
