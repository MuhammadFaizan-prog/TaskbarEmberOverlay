using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using TaskbarEmberOverlay.Interop;
using TaskbarEmberOverlay.Models;

namespace TaskbarEmberOverlay.Services
{
    public class TaskbarTrackerService : IDisposable
    {
        public event Action<List<TaskbarBounds>>? TaskbarsUpdated;

        private readonly DispatcherTimer _pollTimer;
        private uint _taskbarCreatedMsg;
        private List<TaskbarBounds> _lastBounds = new();
        private bool _disposed;

        public TaskbarTrackerService()
        {
            try
            {
                _taskbarCreatedMsg = NativeMethods.RegisterWindowMessage("TaskbarCreated");
            }
            catch
            {
                _taskbarCreatedMsg = 0;
            }

            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _pollTimer.Tick += OnPollTick;
        }

        public void Start()
        {
            _pollTimer.Start();
            RefreshNow();
        }

        public void Stop()
        {
            _pollTimer.Stop();
        }

        private void OnPollTick(object? sender, EventArgs e)
        {
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (_disposed) return;

            var current = EnumerateTaskbars();
            if (HasChanged(_lastBounds, current))
            {
                _lastBounds = current;
                TaskbarsUpdated?.Invoke(current);
            }
        }

        public void HandleWindowMessage(int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == NativeMethods.WM_SETTINGCHANGE ||
                msg == NativeMethods.WM_DISPLAYCHANGE ||
                (msg == _taskbarCreatedMsg && _taskbarCreatedMsg != 0))
            {
                RefreshNow();
            }
        }

        public List<TaskbarBounds> EnumerateTaskbars()
        {
            var results = new List<TaskbarBounds>();

            // 1. Primary Taskbar: Shell_TrayWnd
            IntPtr primaryHwnd = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (primaryHwnd != IntPtr.Zero && NativeMethods.IsWindow(primaryHwnd))
            {
                var primaryBounds = EvaluateTaskbar(primaryHwnd, isPrimary: true);
                if (primaryBounds != null)
                {
                    results.Add(primaryBounds);
                }
            }
            else
            {
                AppLogger.Log($"EnumerateTaskbars: Shell_TrayWnd not found (primaryHwnd={primaryHwnd})");
            }

            // 2. Secondary Taskbars: Shell_SecondaryTrayWnd
            IntPtr secHwnd = IntPtr.Zero;
            while (true)
            {
                secHwnd = NativeMethods.FindWindowEx(IntPtr.Zero, secHwnd, "Shell_SecondaryTrayWnd", null);
                if (secHwnd == IntPtr.Zero) break;

                if (NativeMethods.IsWindow(secHwnd))
                {
                    var secBounds = EvaluateTaskbar(secHwnd, isPrimary: false);
                    if (secBounds != null)
                    {
                        results.Add(secBounds);
                    }
                }
            }

            return results;
        }

        private TaskbarBounds? EvaluateTaskbar(IntPtr hwnd, bool isPrimary)
        {
            if (!NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT rect))
            {
                return null;
            }

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return null;
            }

            bool isVisible = NativeMethods.IsWindowVisible(hwnd);

            // Check auto-hide
            bool isAutoHide = false;
            try
            {
                var appBarData = new NativeMethods.APPBARDATA
                {
                    cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                    hWnd = hwnd
                };
                UIntPtr state = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref appBarData);
                isAutoHide = ((uint)state & NativeMethods.ABS_AUTOHIDE) != 0;
            }
            catch
            {
                // Fallback
            }

            // Retrieve DPI
            uint dpi = 96;
            try
            {
                dpi = NativeMethods.GetDpiForWindow(hwnd);
                if (dpi == 0) dpi = 96;
            }
            catch
            {
                dpi = 96;
            }

            double scale = TaskbarBounds.DpiToScale(dpi);
            var dipRect = TaskbarBounds.CalculateDipRect(rect, scale);

            // Fullscreen check for monitor
            bool isFullscreenCovered = IsFullscreenWindowActiveOnMonitor(hwnd);

            bool eligibleToShow = isVisible && !isFullscreenCovered;
            AppLogger.Log($"EvaluateTaskbar HWND={hwnd}: isVis={isVisible}, fullCover={isFullscreenCovered}, eligible={eligibleToShow}, rect={dipRect}");

            return new TaskbarBounds
            {
                Hwnd = hwnd,
                IsPrimary = isPrimary,
                PhysicalRect = rect,
                DpiScale = scale,
                DipRect = dipRect,
                IsVisible = eligibleToShow,
                IsAutoHide = isAutoHide
            };
        }

        private bool IsFullscreenWindowActiveOnMonitor(IntPtr taskbarHwnd)
        {
            IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
            if (fgHwnd == IntPtr.Zero || fgHwnd == taskbarHwnd)
            {
                return false;
            }

            // Ignore desktop shell window
            IntPtr shellHwnd = NativeMethods.GetShellWindow();
            if (fgHwnd == shellHwnd)
            {
                return false;
            }

            // Ignore desktop wallpaper / icons background (WorkerW, Progman) and taskbar classes
            var clsSb = new System.Text.StringBuilder(256);
            NativeMethods.GetClassName(fgHwnd, clsSb, 256);
            string cls = clsSb.ToString();
            if (cls == "WorkerW" || cls == "Progman" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd" || cls.StartsWith("Shell_"))
            {
                return false;
            }

            // Any window belonging to explorer.exe is part of the Windows desktop / shell
            try
            {
                NativeMethods.GetWindowThreadProcessId(fgHwnd, out uint pid);
                if (pid != 0)
                {
                    using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                    if (proc.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }
            catch { }

            // Get monitor of foreground window and taskbar
            IntPtr fgMon = NativeMethods.MonitorFromWindow(fgHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            IntPtr tbMon = NativeMethods.MonitorFromWindow(taskbarHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

            if (fgMon != tbMon)
            {
                return false;
            }

            // Compare foreground window rect with monitor rect and taskbar rect
            var mi = new NativeMethods.MONITORINFO
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };

            if (NativeMethods.GetMonitorInfo(fgMon, ref mi) && 
                NativeMethods.GetWindowRect(fgHwnd, out NativeMethods.RECT fgRect) &&
                NativeMethods.GetWindowRect(taskbarHwnd, out NativeMethods.RECT tbRect))
            {
                // If the taskbar is actively displayed on screen, a normal maximized window
                // (e.g. Chrome, Explorer, Antigravity) does not occlude the taskbar.
                // Only a topmost exclusive fullscreen window (like a full-screen game or video) can occlude it.
                if (tbRect.Top < mi.rcMonitor.Bottom - 8 && tbRect.Bottom > mi.rcMonitor.Top + 8)
                {
                    long fgExStyle = NativeMethods.GetWindowLongPtr(fgHwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
                    bool isTopmost = (fgExStyle & NativeMethods.WS_EX_TOPMOST) != 0;
                    if (!isTopmost)
                    {
                        return false;
                    }
                }

                // A window is only fullscreen covering the taskbar if:
                // 1) It covers the entire monitor bounds
                // 2) It actually covers the taskbar rectangle
                bool coversMonitor = fgRect.Left <= mi.rcMonitor.Left &&
                                     fgRect.Top <= mi.rcMonitor.Top &&
                                     fgRect.Right >= mi.rcMonitor.Right &&
                                     fgRect.Bottom >= mi.rcMonitor.Bottom;

                bool coversTaskbar = fgRect.Left <= tbRect.Left &&
                                     fgRect.Top <= tbRect.Top &&
                                     fgRect.Right >= tbRect.Right &&
                                     fgRect.Bottom >= tbRect.Bottom;

                if (coversMonitor && coversTaskbar)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasChanged(List<TaskbarBounds> oldList, List<TaskbarBounds> newList)
        {
            if (oldList.Count != newList.Count) return true;

            for (int i = 0; i < newList.Count; i++)
            {
                var n = newList[i];
                var o = oldList.FirstOrDefault(x => x.Hwnd == n.Hwnd);
                if (o == null) return true;

                if (o.PhysicalRect.Left != n.PhysicalRect.Left ||
                    o.PhysicalRect.Top != n.PhysicalRect.Top ||
                    o.PhysicalRect.Right != n.PhysicalRect.Right ||
                    o.PhysicalRect.Bottom != n.PhysicalRect.Bottom ||
                    Math.Abs(o.DpiScale - n.DpiScale) > 0.001 ||
                    o.IsVisible != n.IsVisible ||
                    o.IsAutoHide != n.IsAutoHide)
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            _disposed = true;
            _pollTimer.Stop();
        }
    }
}
