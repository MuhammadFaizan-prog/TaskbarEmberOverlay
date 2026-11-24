# Project Structure (Structure)

```
TaskbarEmberOverlay/
├── PRD.md
├── Techspec.md
├── Appflow.md
├── Schema.md
├── ImplementationPlan.md
├── Tracker.md
├── Rules.md
├── Security.md
├── Structure.md
├── README.md
├── TaskbarEmberOverlay.sln
├── src/
│   └── TaskbarEmberOverlay/
│       ├── TaskbarEmberOverlay.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── OverlayWindow.xaml
│       ├── OverlayWindow.xaml.cs
│       ├── Models/
│       │   ├── AppSettings.cs
│       │   ├── TaskbarInfo.cs
│       │   └── EmberParticle.cs
│       ├── Services/
│       │   ├── SettingsService.cs
│       │   ├── SingleInstanceService.cs
│       │   ├── TaskbarTrackerService.cs
│       │   └── EmberAnimationEngine.cs
│       └── Interop/
│           └── NativeMethods.cs
├── tests/
│   └── TaskbarEmberOverlay.Tests/
│       ├── TaskbarEmberOverlay.Tests.csproj
│       ├── GeometryTests.cs
│       ├── DpiConversionTests.cs
│       ├── SettingsTests.cs
│       ├── IpcCommandTests.cs
│       └── AnimationTimingTests.cs
└── artifacts/
    └── publish/
        └── win-x64/
            └── TaskbarEmberOverlay.exe
```
