using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using Microsoft.Win32;
using TaskbarEmberOverlay.Interop;
using TaskbarEmberOverlay.Models;

namespace TaskbarEmberOverlay.Services
{
    public static class TaskbarLayoutDetector
    {
        private static DateTime _lastDetectionTime = DateTime.MinValue;
        private static List<Rect> _cachedFreeSpaces = new();
        private static Rect _lastTaskbarDipRect = Rect.Empty;

        public static List<Rect> DetectFreeSpaces(IntPtr trayHwnd, Rect taskbarDipRect, double dpiScale, bool forceRefresh = false)
        {
            if (taskbarDipRect.Width <= 0 || taskbarDipRect.Height <= 0)
            {
                return new List<Rect>();
            }

            // Quick cache check (1.0 seconds) to avoid repeated scans while rendering
            if (!forceRefresh && (DateTime.UtcNow - _lastDetectionTime).TotalSeconds < 1.0 && _lastTaskbarDipRect == taskbarDipRect)
            {
                return new List<Rect>(_cachedFreeSpaces);
            }

            var freeSpaces = new List<Rect>();
            double tbWidth = taskbarDipRect.Width;
            double tbHeight = taskbarDipRect.Height;

            // 1. Detect Taskbar physical origin and System Tray on the right
            NativeMethods.GetWindowRect(trayHwnd, out var trayPhysRect);
            double physTrayLeft = trayPhysRect.Left;

            IntPtr trayNotifyHwnd = NativeMethods.FindWindowEx(trayHwnd, IntPtr.Zero, "TrayNotifyWnd", null);
            Rect trayDipRect = Rect.Empty;
            double trayNotifyLeft = 0;
            if (trayNotifyHwnd != IntPtr.Zero && NativeMethods.GetWindowRect(trayNotifyHwnd, out var trayNotifyPhysRect))
            {
                trayDipRect = TaskbarBounds.CalculateDipRect(trayNotifyPhysRect, dpiScale);
                trayNotifyLeft = trayNotifyPhysRect.Left;
            }

            double localTrayLeft = !trayDipRect.IsEmpty && trayDipRect.Width > 0 
                ? ((trayNotifyLeft - physTrayLeft) / dpiScale)
                : (tbWidth - 240);

            // 2. Check Taskbar Alignment (0 = Left, 1 = Center)
            bool isCenterAligned = true;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                if (key != null)
                {
                    var val = key.GetValue("TaskbarAl");
                    if (val is int intVal && intVal == 0)
                    {
                        isCenterAligned = false;
                    }
                }
            }
            catch { }

            // 3. Fast Win32 Direct Child Check (Start button, ReBarWindow32 app list, TrayNotifyWnd)
            double weatherRight = 0;
            double startLeft = 0;
            double appListRight = 0;
            bool foundAppList = false;

            try
            {
                if (trayHwnd != IntPtr.Zero)
                {
                    IntPtr startHwnd = NativeMethods.FindWindowEx(trayHwnd, IntPtr.Zero, "Start", null);
                    if (startHwnd != IntPtr.Zero && NativeMethods.GetWindowRect(startHwnd, out var startRect) && startRect.Width > 10)
                    {
                        startLeft = Math.Max(0, (startRect.Left - physTrayLeft) / dpiScale);
                    }

                    IntPtr rebarHwnd = NativeMethods.FindWindowEx(trayHwnd, IntPtr.Zero, "ReBarWindow32", null);
                    if (rebarHwnd != IntPtr.Zero && NativeMethods.GetWindowRect(rebarHwnd, out var rebarRect))
                    {
                        if (rebarRect.Right > physTrayLeft)
                        {
                            appListRight = Math.Min(tbWidth, (rebarRect.Right - physTrayLeft) / dpiScale);
                            foundAppList = true;
                        }
                    }
                }
            }
            catch { }

            // 4. Fallback / supplementary UI Automation for StartButton, AppList, and WidgetsButton
            try
            {
                if (trayHwnd != IntPtr.Zero && (!foundAppList || startLeft <= 0))
                {
                    var trayElem = AutomationElement.FromHandle(trayHwnd);
                    if (trayElem != null)
                    {
                        // Fast targeted find for Weather
                        var widgetElem = trayElem.FindFirst(TreeScope.Descendants, 
                            new PropertyCondition(AutomationElement.AutomationIdProperty, "WidgetsButton"));
                        if (widgetElem != null)
                        {
                            var wr = widgetElem.Current.BoundingRectangle;
                            if (wr.Width > 15 && wr.Height > 10)
                            {
                                weatherRight = Math.Max(0, (wr.Right - physTrayLeft) / dpiScale);
                            }
                        }

                        // Fast targeted find for Start button
                        var startElem = trayElem.FindFirst(TreeScope.Descendants, 
                            new PropertyCondition(AutomationElement.AutomationIdProperty, "StartButton"));
                        if (startElem != null)
                        {
                            var sr = startElem.Current.BoundingRectangle;
                            if (sr.Width > 15 && sr.Height > 10)
                            {
                                startLeft = Math.Max(0, (sr.Left - physTrayLeft) / dpiScale);
                            }
                        }

                        // Fast targeted find for AppList (holds all running & pinned icons)
                        var appListElem = trayElem.FindFirst(TreeScope.Descendants, 
                            new PropertyCondition(AutomationElement.AutomationIdProperty, "AppList"));
                        if (appListElem != null)
                        {
                            var ar = appListElem.Current.BoundingRectangle;
                            double rightEdge = ar.Right;

                            // Dynamic detection: check child buttons inside AppList to find the real rightmost icon
                            try
                            {
                                var children = appListElem.FindAll(TreeScope.Children, System.Windows.Automation.Condition.TrueCondition);
                                if (children != null && children.Count > 0)
                                {
                                    double maxChildRight = 0;
                                    foreach (AutomationElement ch in children)
                                    {
                                        var cr = ch.Current.BoundingRectangle;
                                        if (cr.Width > 8 && cr.Right > maxChildRight)
                                        {
                                            maxChildRight = cr.Right;
                                        }
                                    }
                                    if (maxChildRight > physTrayLeft)
                                    {
                                        rightEdge = maxChildRight;
                                    }
                                }
                            }
                            catch { }

                            if (rightEdge > physTrayLeft)
                            {
                                appListRight = Math.Min(tbWidth, (rightEdge - physTrayLeft) / dpiScale);
                                foundAppList = true;
                            }
                        }
                    }
                }
            }
            catch { }

            // Intelligent fallbacks if UI Automation is not available
            if (weatherRight <= 0)
            {
                weatherRight = isCenterAligned ? 140 : 0;
            }

            if (!foundAppList || startLeft <= 0 || appListRight <= 0)
            {
                if (isCenterAligned)
                {
                    if (startLeft > 0 && appListRight <= 0)
                    {
                        appListRight = Math.Min(localTrayLeft - 30, tbWidth - startLeft);
                    }
                    else
                    {
                        // Dynamic estimation: on modern displays with ~16 icons, cluster is ~680 DIPs wide
                        double estCluster = Math.Min(680, tbWidth * 0.45);
                        startLeft = (tbWidth - estCluster) / 2.0;
                        appListRight = startLeft + estCluster;
                    }
                }
                else
                {
                    startLeft = weatherRight > 0 ? weatherRight + 6 : 6;
                    appListRight = Math.Min(localTrayLeft - 30, startLeft + Math.Min(650, tbWidth * 0.45));
                }
            }

            // Tight padding (2 DIPs) so the animation fills the empty space without harsh black borders
            double pad = 2.0;

            if (isCenterAligned)
            {
                // Free Space 1: Between Weather and Start button
                double free1Start = weatherRight > 0 ? (weatherRight + pad) : 2.0;
                double free1End = startLeft > 0 ? (startLeft - pad) : (tbWidth * 0.3);
                double free1Width = free1End - free1Start;
                if (free1Width > 15)
                {
                    freeSpaces.Add(new Rect(free1Start, 2, free1Width, tbHeight - 4));
                }

                // Free Space 2: Between pinned apps and System Tray
                double free2Start = appListRight > 0 ? (appListRight + pad) : (tbWidth * 0.7);
                double free2End = localTrayLeft - pad;
                double free2Width = free2End - free2Start;
                if (free2Width > 15)
                {
                    freeSpaces.Add(new Rect(free2Start, 2, free2Width, tbHeight - 4));
                }
            }
            else
            {
                // Left aligned: Free space between end of apps and system tray
                double freeStart = appListRight > 0 ? (appListRight + pad) : (tbWidth * 0.5);
                double freeEnd = localTrayLeft - pad;
                double freeWidth = freeEnd - freeStart;
                if (freeWidth > 15)
                {
                    freeSpaces.Add(new Rect(freeStart, 2, freeWidth, tbHeight - 4));
                }
            }

            _cachedFreeSpaces = new List<Rect>(freeSpaces);
            _lastDetectionTime = DateTime.UtcNow;
            _lastTaskbarDipRect = taskbarDipRect;

            return freeSpaces;
        }
    }
}
