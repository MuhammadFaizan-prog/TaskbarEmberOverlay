# Technical Specification (Techspec)

## Architecture Overview
TaskbarEmberOverlay is a C# .NET 10 desktop application leveraging WPF (`Microsoft.WindowsDesktop.App`) with direct Win32 API interop for low-overhead, hardware-accelerated rendering and window positioning.

```
+-----------------------------------------------------------+
|                      App Entry Point                      |
| (Single-Instance Mutex, CLI Parser [--exit, --pause], IPC) |
+-----------------------------------------------------------+
                              |
       +----------------------+----------------------+
       |                                             |
+---------------+                             +---------------+
| SettingsModel |                             | TaskbarManager|
| (JSON Config) |                             | (Win32 P/Inv) |
+---------------+                             +---------------+
                                                     |
                                      Enumerates Taskbar Handles:
                                      - Shell_TrayWnd (Primary)
                                      - Shell_SecondaryTrayWnd
                                                     |
                                            Per Monitor Instance
                                                     |
                                         +-----------------------+
                                         | TaskbarOverlayWindow  |
                                         | - WS_EX_TRANSPARENT   |
                                         | - WS_EX_LAYERED       |
                                         | - WS_EX_TOOLWINDOW    |
                                         | - WS_EX_NOACTIVATE    |
                                         +-----------------------+
                                                     |
                                         +-----------------------+
                                         | ProceduralEmberCanvas |
                                         | - CombinedGeometry    |
                                         |   (Exclude Interior)  |
                                         | - Ember Crackles      |
                                         | - Drifting Sparks     |
                                         | - Render Loop (30fps) |
                                         +-----------------------+
```

## Win32 Window Interop
1. **Window Styles**:
   - `WS_EX_TRANSPARENT` (0x00000020): Pass all hit tests to windows below.
   - `WS_EX_LAYERED` (0x00080000): Support per-pixel alpha transparency.
   - `WS_EX_TOOLWINDOW` (0x00000080): Hide from Alt+Tab and taskbar.
   - `WS_EX_NOACTIVATE` (0x08000000): Prevent taking focus on click or show.
2. **Hit Testing**:
   - Subclass `HwndSource` with `Hook`:
     - Intercept `WM_NCHITTEST` (0x0084) and return `HTTRANSPARENT` (-1).
3. **Z-Order Placement**:
   - Kept directly tied to the taskbar. Not set to `HWND_TOPMOST` globally so full-screen applications and modal dialogs are not covered.
4. **Taskbar Tracking**:
   - P/Invoke `FindWindow("Shell_TrayWnd", null)` for primary taskbar.
   - P/Invoke `FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_SecondaryTrayWnd", null)` for secondary taskbars.
   - `GetWindowRect` to fetch physical coordinates.
   - Convert physical pixels to WPF DIPs using `VisualTreeHelper` / `DpiForWindow` / `GetDpiForMonitor`.
   - Register for `TaskbarCreated` broadcast message (`RegisterWindowMessage("TaskbarCreated")`) for Explorer restart recovery.
   - Listen to `WM_SETTINGCHANGE` (`SPI_SETWORKAREA`), `WM_DISPLAYCHANGE`.
   - Low-frequency heartbeat timer (750ms) to detect movement, resolution shifts, or auto-hide status changes.

## Rendering Pipeline
1. **Perimeter Clipping (`CombinedGeometry`)**:
   - `outerRect = new Rect(0, 0, width, height);`
   - `innerRect = new Rect(edgeThickness, edgeThickness, width - 2*edgeThickness, height - 2*edgeThickness);`
   - `clipGeometry = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(outerRect), new RectangleGeometry(innerRect));`
   - Canvas `Clip = clipGeometry;`
   - This mathematically guarantees that no pixel or particle can ever render inside the taskbar interior.
2. **Procedural Ember Animation**:
   - Segment-based molten crack network along the perimeter lines.
   - Micro-flicker: high-frequency noise modulation of line luminance (hot gold, fiery orange, deep crimson).
   - Occasional drifting ember sparks: 15-25 particles with short lifespans, moving along edge tangents and fading out before escaping the perimeter.
   - Capped at 30 fps via `CompositionTarget.Rendering` with delta-time throttling.
   - Pauses rendering loop when window is hidden (`Visibility.Hidden` or minimized).
3. **Reduced Motion**:
   - Detects `SystemParameters.HighContrast` or `SystemParameters.ClientAreaAnimation`.
   - In static mode, generates a fixed glowing molten crackle texture along the edge without motion or particle updates, saving 100% of animation CPU cycles.

## IPC & Single Instance
- `System.Threading.Mutex` (`TaskbarEmberOverlay_SingleInstance_Mutex`) for primary instance lock.
- `NamedPipeServerStream` (`TaskbarEmberOverlay_Ipc`) running in background thread listening for commands:
  - `EXIT`: Triggers graceful application shutdown (`Application.Current.Shutdown()`).
  - `PAUSE`: Pauses animation rendering loop.
  - `RESUME`: Resumes animation rendering loop.
  - `PING`: Verifies process alive.
- Invocation with `--exit` opens `NamedPipeClientStream`, writes `EXIT`, and exits immediately with code 0.
