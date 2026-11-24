# Application Flow (Appflow)

## Lifecycle Flow

```
[Start TaskbarEmberOverlay.exe]
           |
   Parse Arguments?
    /            \
[--exit]      [Normal Launch]
   |                 |
Send "EXIT"     Acquire Single-Instance Mutex
via NamedPipe   (If already running -> exit 0)
   |                 |
Exit 0          Load Settings from %LOCALAPPDATA%\TaskbarEmberOverlay\settings.json
                     |
                Start IPC Named Pipe Server
                     |
                Query Windows Taskbars (Shell_TrayWnd & Secondary)
                     |
                Create Overlay Window(s) for Each Taskbar
                - Apply WS_EX_TRANSPARENT, WS_EX_LAYERED, WS_EX_TOOLWINDOW
                - Set Bounds to match taskbar rect
                - Apply CombinedGeometry (Exclude Interior)
                     |
                Start Render Loop (30 FPS capped)
                     |
      +--------------+--------------+
      |                             |
[User Interacts With Taskbar]  [System Events]
- Clicks, hovers, drags        - Display resolution changes
  pass straight through        - Taskbar autohides / reveals
  to Explorer. Zero            - Fullscreen app activates
  focus stealing.              - Explorer restarts ("TaskbarCreated")
                                    |
                               Reposition/Resize/Hide/Show Overlays
```

## State Machine
- **INITIALIZING**: Mutex acquired, settings parsed, Win32 hooks established.
- **ACTIVE_RUNNING**: Overlays visible, tracking taskbar bounds, 30fps animation loop active.
- **PAUSED_HIDDEN**: Taskbar is hidden (auto-hide), display is locked, or fullscreen application is active. Render loop paused, 0% CPU consumption.
- **STATIC_MODE**: User or OS requested reduced motion. Perimeter renders static molten glow without active particle loop.
- **TERMINATING**: IPC "EXIT" or OS shutdown received. Resources disposed, windows closed, mutex released.
