# Project Rules (Rules)

## Development & Code Guidelines
1. **Never Intercept Mouse or Keyboard**:
   - The overlay must NEVER handle mouse input or receive focus.
   - `WS_EX_TRANSPARENT`, `WS_EX_LAYERED`, `WS_EX_NOACTIVATE`, and `WS_EX_TOOLWINDOW` must be applied to all overlay windows immediately upon creation.
   - `WM_NCHITTEST` must return `HTTRANSPARENT`.
2. **Never Render Over Taskbar Interior**:
   - All drawing operations must be masked or bounded by `CombinedGeometry` (outer rect minus inner rect).
   - Inner rectangle margin must strictly match or exceed `EdgeThickness`.
3. **No Registry, No Admin, No Background Services**:
   - Do not touch registry keys (`HKCU\...\Run`, etc.).
   - Do not register Windows Services.
   - Auto-start must only be configured via user startup shortcut (`shell:startup`) as documented.
4. **Clean Disposal & Low Overhead**:
   - Limit rendering to at most 30 FPS.
   - Stop animation timers / rendering hooks when overlays are hidden or minimized.
   - Dispose all Win32 handles, brushes, and streams on exit.
5. **Reversibility**:
   - The app must be fully uninstallable simply by deleting its install folder in `%LOCALAPPDATA%\Programs\TaskbarEmberOverlay` and settings in `%LOCALAPPDATA%\TaskbarEmberOverlay`.
   - Never modify or overwrite files in the parent `Downloads\custom taskbar` folder.
