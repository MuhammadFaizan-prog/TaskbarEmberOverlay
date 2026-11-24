using System;
using System.Runtime.InteropServices;
using System.Threading;
using TaskbarEmberOverlay.Interop;
using TaskbarEmberOverlay.Services;

namespace TaskbarEmberOverlay
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            AppLogger.Log($"Program.Main started. CLI args: [{string.Join(", ", args)}]");

            // Handle CLI quick commands (--exit, --pause, --resume, --help) directly
            if (args.Length > 0)
            {
                string arg = args[0].Trim().ToLowerInvariant();
                if (arg is "--exit" or "-exit" or "/exit" or "--pause" or "-pause" or "--resume" or "-resume" or "--help" or "-help" or "-h" or "/?")
                {
                    var quickApp = new App();
                    quickApp.InitializeComponent();
                    quickApp.Run();
                    return;
                }
            }

            // Launch WPF UI on a fresh STA thread that has zero prior windows/hooks,
            // allowing SetThreadDesktop to succeed 100% and bind to the interactive 'Default' desktop
            var uiThread = new Thread(() =>
            {
                try
                {
                    IntPtr hDesk = NativeMethods.OpenDesktop("Default", 0, false, NativeMethods.DESKTOP_ALL);
                    if (hDesk != IntPtr.Zero)
                    {
                        bool set = NativeMethods.SetThreadDesktop(hDesk);
                        int err = Marshal.GetLastWin32Error();
                        AppLogger.Log($"UI Thread SetThreadDesktop(Default) returned: {set}, LastError={err}, handle={hDesk}");
                    }
                    else
                    {
                        int err = Marshal.GetLastWin32Error();
                        AppLogger.Log($"OpenDesktop(Default) returned Zero, LastError={err}");
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Log($"SetThreadDesktop exception: {ex}");
                }

                try
                {
                    var app = new App();
                    app.InitializeComponent();
                    app.Run();
                }
                catch (Exception ex)
                {
                    AppLogger.Log($"Fatal App.Run exception: {ex}");
                }
            });

            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();
            uiThread.Join();
        }
    }
}
