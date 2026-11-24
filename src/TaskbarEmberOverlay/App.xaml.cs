using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TaskbarEmberOverlay.Models;
using TaskbarEmberOverlay.Services;

namespace TaskbarEmberOverlay
{
    public partial class App : Application
    {
        private SingleInstanceService? _singleInstanceService;
        private SettingsService? _settingsService;
        private TaskbarTrackerService? _trackerService;
        private AppSettings _settings = new();

        private readonly Dictionary<IntPtr, OverlayWindow> _activeOverlays = new();
        private bool _isPaused;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            EnsureInteractiveDesktop();
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                try
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarEmberOverlay", "crash.log"),
                        $"[{DateTime.Now}] Unhandled Exception: {args.ExceptionObject}\n");
                }
                catch { }
            };

            DispatcherUnhandledException += (s, args) =>
            {
                try
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarEmberOverlay", "crash.log"),
                        $"[{DateTime.Now}] Dispatcher Exception: {args.Exception}\n");
                }
                catch { }
                args.Handled = true;
            };

            // Handle CLI arguments
            if (e.Args.Length > 0)
            {
                string arg = e.Args[0].Trim().ToLowerInvariant();
                switch (arg)
                {
                    case "--exit":
                    case "-exit":
                    case "/exit":
                        await HandleExitCommandAsync();
                        Shutdown(0);
                        return;

                    case "--pause":
                    case "-pause":
                        await HandleIpcCommandAsync("PAUSE");
                        Shutdown(0);
                        return;

                    case "--resume":
                    case "-resume":
                        await HandleIpcCommandAsync("RESUME");
                        Shutdown(0);
                        return;

                    case "--help":
                    case "-help":
                    case "-h":
                    case "/?":
                        Console.WriteLine("TaskbarEmberOverlay CLI Options:");
                        Console.WriteLine("  --exit    Gracefully shuts down the running overlay instance.");
                        Console.WriteLine("  --pause   Pauses the rendering loop.");
                        Console.WriteLine("  --resume  Resumes the rendering loop.");
                        Console.WriteLine("  --help    Displays this help information.");
                        Shutdown(0);
                        return;
                }
            }

            // Normal Startup: check single instance
            _singleInstanceService = new SingleInstanceService();
            if (!_singleInstanceService.TryAcquire(out bool isFirst) || !isFirst)
            {
                // Another instance is already running
                Shutdown(0);
                return;
            }

            try
            {
                // Load settings
                _settingsService = new SettingsService();
                _settings = _settingsService.LoadOrCreate();

                // Wire up IPC
                _singleInstanceService.ExitRequested += OnExitRequested;
                _singleInstanceService.PauseRequested += OnPauseRequested;
                _singleInstanceService.ResumeRequested += OnResumeRequested;
                _singleInstanceService.StartIpcServer();

                // Wire up taskbar tracking
                _trackerService = new TaskbarTrackerService();
                _trackerService.TaskbarsUpdated += OnTaskbarsUpdated;
                _trackerService.Start();
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarEmberOverlay", "app.log"),
                    $"[{DateTime.Now}] Startup error: {ex}\n");
            }
        }

        private static void EnsureInteractiveDesktop()
        {
            try
            {
                IntPtr hDesk = TaskbarEmberOverlay.Interop.NativeMethods.OpenInputDesktop(0, false, TaskbarEmberOverlay.Interop.NativeMethods.DESKTOP_ALL);
                if (hDesk == IntPtr.Zero)
                {
                    hDesk = TaskbarEmberOverlay.Interop.NativeMethods.OpenDesktop("Default", 0, false, TaskbarEmberOverlay.Interop.NativeMethods.DESKTOP_ALL);
                }
                if (hDesk != IntPtr.Zero)
                {
                    TaskbarEmberOverlay.Interop.NativeMethods.SetThreadDesktop(hDesk);
                }
            }
            catch { }
        }

        private static async Task HandleExitCommandAsync()
        {
            string? response = await SingleInstanceService.SendCommandAsync("EXIT");
            if (response == "OK_EXITING")
            {
                Console.WriteLine("TaskbarEmberOverlay: Successfully requested shutdown.");
            }
            else
            {
                Console.WriteLine("TaskbarEmberOverlay: No running instance detected or response timed out.");
            }
        }

        private static async Task HandleIpcCommandAsync(string cmd)
        {
            string? response = await SingleInstanceService.SendCommandAsync(cmd);
            Console.WriteLine($"TaskbarEmberOverlay: Command {cmd} result: {response ?? "Timed out"}");
        }

        private void OnTaskbarsUpdated(List<TaskbarBounds> taskbars)
        {
            Dispatcher.Invoke(() =>
            {
                var currentHwnds = new HashSet<IntPtr>(taskbars.Select(t => t.Hwnd));

                // Remove overlays for closed taskbars
                var toRemove = _activeOverlays.Keys.Where(h => !currentHwnds.Contains(h)).ToList();
                foreach (var h in toRemove)
                {
                    if (_activeOverlays.TryGetValue(h, out var win))
                    {
                        win.Close();
                        _activeOverlays.Remove(h);
                    }
                }

                // Add or update overlays for active taskbars
                foreach (var tb in taskbars)
                {
                    if (!_activeOverlays.TryGetValue(tb.Hwnd, out var win))
                    {
                        win = new OverlayWindow(_settings);
                        win.SetPaused(_isPaused);
                        _activeOverlays[tb.Hwnd] = win;
                        win.Show();
                    }

                    win.UpdateTaskbarBounds(tb);
                }
            });
        }

        private void OnExitRequested()
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarEmberOverlay", "exit.log"),
                $"[{DateTime.Now}] App.OnExitRequested executing shutdown\n");
            Dispatcher.Invoke(() =>
            {
                Shutdown(0);
            });
            Task.Delay(500).ContinueWith(_ => Environment.Exit(0));
        }

        private void OnPauseRequested()
        {
            Dispatcher.Invoke(() =>
            {
                _isPaused = true;
                foreach (var win in _activeOverlays.Values)
                {
                    win.SetPaused(true);
                }
            });
        }

        private void OnResumeRequested()
        {
            Dispatcher.Invoke(() =>
            {
                _isPaused = false;
                foreach (var win in _activeOverlays.Values)
                {
                    win.SetPaused(false);
                }
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trackerService?.Dispose();

            foreach (var win in _activeOverlays.Values)
            {
                try { win.Close(); } catch { }
            }
            _activeOverlays.Clear();

            _singleInstanceService?.Dispose();
            base.OnExit(e);
        }
    }
}
