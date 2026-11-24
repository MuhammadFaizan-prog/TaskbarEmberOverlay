# Implementation Plan

## Milestones & Phasing

### Phase 1: Reference Analysis & Criteria [Complete]
- Examined reference video `side animation for taskbar.mp4` and user recording `19.11.2026_23.22.40_REC.mp4`.
- Analyzed color profile (mean top RGB [180, 100, 60] with bright [230, 180, 135] hot sparks).
- Confirmed strict boundary requirement: 0% interior fill, 100% click-through.

### Phase 2: Documentation & Memory Architecture [In Progress]
- Draft 9 required project memory documents in project root.
- Document P/Invoke definitions, exclusion clipping math, and single-instance IPC.

### Phase 3: Visual Proof / Prototype Verification
- Generate sample rendered taskbar preview with transparent interior and perimeter ember lines.
- Verify exclusion geometry and alpha channel on checkerboard.

### Phase 4: Solution & Project Scaffolding
- Create `TaskbarEmberOverlay.sln`.
- Create `src/TaskbarEmberOverlay/TaskbarEmberOverlay.csproj` targeting `net10.0-windows` with WPF enabled.
- Create `tests/TaskbarEmberOverlay.Tests/TaskbarEmberOverlay.Tests.csproj` with xUnit.

### Phase 5: Core Implementation
1. **Interop**:
   - `NativeMethods.cs`: `FindWindow`, `FindWindowEx`, `GetWindowRect`, `SetWindowPos`, `SetWindowLongPtr`, `GetWindowLongPtr`, `RegisterWindowMessage`, `SHAppBarMessage`.
2. **Taskbar Management**:
   - `TaskbarMonitor.cs`: tracks active displays, bounds, DPI scale, autohide status, fullscreen app collision, Explorer restarts.
3. **Window & Hit-Testing**:
   - `OverlayWindow.xaml.cs`: Transparent, non-activating, tool window with `WS_EX_TRANSPARENT` and `WM_NCHITTEST` returning `HTTRANSPARENT`.
4. **Perimeter Ember Engine**:
   - `EmberRenderer.cs`: Procedural molten crack generator, glowing nodes, drifting sparks, exclusion geometry clip, 30fps capped loop.
5. **Configuration & Single-Instance**:
   - `SettingsService.cs`: Atomic load/save at `%LOCALAPPDATA%\TaskbarEmberOverlay\settings.json`.
   - `SingleInstanceService.cs`: Named Mutex and Named Pipe IPC handling `--exit`, `--pause`, `--resume`.

### Phase 6: Automated Testing
- Unit tests for geometry clipping calculations.
- Unit tests for DPI physical-to-DIP conversions.
- Unit tests for settings parsing, schema defaults, and fallback clamping.
- Unit tests for CLI argument parsing and IPC message protocol.
- Unit tests for animation tick safety and bounds clamping.

### Phase 7: Build, Publish & Local Live Verification
- Build Release configuration: `dotnet build -c Release`.
- Run tests: `dotnet test -c Release`.
- Publish self-contained executable: `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o artifacts/publish/win-x64`.
- Run live `TaskbarEmberOverlay.exe`, verify taskbar responsiveness (Start, Search, icons, clock), measure CPU/RAM, and test `--exit`.
- Create comprehensive README and verification report.
