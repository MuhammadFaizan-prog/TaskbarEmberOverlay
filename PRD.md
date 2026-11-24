# Product Requirements Document (PRD)

## Product Summary
**TaskbarEmberOverlay** is a lightweight, reversible Windows desktop application built with C# and WPF on .NET 10 (LTS). It renders a subtle, decorative ember/crackle/spark animation running exclusively around the rectangular outer perimeter of the Windows 10/11 taskbar on all active displays.

## Target Users
Windows 10/11 power users, desktop customizers, and enthusiasts who want aesthetic, game-themed border decoration around their taskbar without compromising system performance, security, or taskbar usability.

## Core Problem
Existing animated wallpaper or desktop overlay tools often:
1. Cover the entire taskbar with opaque or semi-transparent video layers that occlude Start menu icons, clock, and system tray.
2. Intercept mouse clicks, breaking Start menu, app switching, search, and notification interactions.
3. Waste high CPU/GPU resources rendering heavy video decoders in continuous loops.
4. Fail across multi-monitor setups, DPI changes, resolution shifts, or when Windows Explorer restarts.

## Goals
- **Perimeter-Only Aesthetics**: Render controlled orange, red, and hot-gold glowing ember cracks and short-lived drifting sparks confined to a narrow outer perimeter band (3-5px).
- **100% Taskbar Usability**: Interior of taskbar remains 100% transparent and non-occluded. All mouse events pass through transparently via `WS_EX_TRANSPARENT` and `WM_NCHITTEST` returning `HTTRANSPARENT`.
- **Zero Input/Focus Interception**: The overlay window never steals keyboard/mouse focus (`WS_EX_NOACTIVATE`), does not appear in Alt+Tab (`WS_EX_TOOLWINDOW`), and does not create taskbar buttons.
- **Dynamic Taskbar Tracking**: Accurately tracks primary and secondary taskbars (`Shell_TrayWnd`, `Shell_SecondaryTrayWnd`), auto-hide states, multi-monitor setups, per-monitor DPI scaling, and Explorer restarts.
- **Resource Efficiency**: Capped frame rate (target 30 fps), pause when taskbar is hidden or covered by full-screen applications, < 1% idle CPU.
- **Accessibility & Clean Exit**: Respects Windows reduced-motion preferences, provides configurable settings in `%LOCALAPPDATA%\TaskbarEmberOverlay\settings.json`, supports clean shutdown via `--exit`, and is 100% reversible.

## Non-Goals
- Full-screen or desktop-wide animated wallpapers.
- Showing the profile card, avatar, labels, or audio from the reference video.
- System-wide hook injection, DLL injection, or modifying Windows Explorer binaries.
- Admin privilege requirements or Windows Service registration.

## Acceptance Criteria
1. Taskbar interior is completely transparent and clear of all particles, smoke, or purple haze.
2. Every taskbar icon and control (Start, Search, pinned apps, system tray, clock, calendar, action center) is 100% responsive to clicks, hovers, right-clicks, and drag-and-drop.
3. Multi-monitor support: overlay creates an aligned perimeter on each connected display with a taskbar.
4. DPI/Resolution responsiveness: dynamically recalculates DIPs when displays change resolution or scale factors.
5. Auto-hide & Fullscreen awareness: overlay hides when taskbar autohides or when a foreground window enters full screen.
6. Explorer recovery: if Explorer restarts, overlay automatically rebinds to the newly created taskbars.
7. Clean exit via `--exit` command.
